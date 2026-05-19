import { useState, useEffect, useRef } from 'react'
import AgentGrid from './AgentGrid'
import ActivityTerminal from './ActivityTerminal'
import SelfImprovementPanel from './SelfImprovementPanel'
import allAgents, { getVoiceId, getAgentField } from '../data/agents'
import { routeMessage } from '../data/responses'
import { t, languages } from '../i18n'

let voicesLoaded = false

export default function MeetingRoom() {
  const [lang, setLang] = useState('tr')
  const [room, setRoom] = useState('strategy')
  const [view, setView] = useState('meeting')
  const [speakingAgentId, setSpeakingAgentId] = useState(null)
  const [caption, setCaption] = useState('')
  const [listening, setListening] = useState(false)
  const [transcript, setTranscript] = useState([])
  const [chatInput, setChatInput] = useState('')
  const [activeCmdId, setActiveCmdId] = useState(null)
  const lastLogTimeRef = useRef(null)
  const recognitionRef = useRef(null)
  const chatEndRef = useRef(null)
  const timerRef = useRef(null)
  const speakingRef = useRef(false)
  const langRef = useRef(lang)
  langRef.current = lang

  useEffect(() => {
    if (!window.speechSynthesis || voicesLoaded) return
    voicesLoaded = true
    const load = () => {
      const v = window.speechSynthesis.getVoices()
      if (v.length > 0) {
        const trVoices = v.filter(x => x.lang.startsWith('tr')).map(x => `${x.name} (${x.lang})`)
        console.log('[Voices] Turkish:', trVoices.length ? trVoices : 'NONE')
        console.log('[Voices] All:', v.map(x => `${x.name} (${x.lang})`))
      }
    }
    load()
    window.speechSynthesis.onvoiceschanged = load
  }, [])

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [transcript])

  useEffect(() => {
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current)
      if (window.speechSynthesis) window.speechSynthesis.cancel()
    }
  }, [])

  useEffect(() => {
    fetch('/api/command/conversations?count=50')
      .then(res => res.json())
      .then(data => {
        if (Array.isArray(data) && data.length > 0) {
          const entries = []
          const reversed = [...data].reverse()
          for (const c of reversed) {
            if (c.room !== room) continue
            entries.push({ who: 'user', text: c.userMessage })
            if (c.hadCodeChange && c.codeResult) {
              entries.push({ who: c.agentId, text: c.agentResponse + ' 🔧 ' + c.codeResult })
            } else {
              entries.push({ who: c.agentId, text: c.agentResponse })
            }
          }
          if (entries.length > 0) setTranscript(entries)
        }
      })
      .catch(e => console.error('History load error:', e))
  }, [])

  useEffect(() => {
    if (!activeCmdId) return

    const poll = async () => {
      try {
        const feedRes = await fetch('/api/command/feed?count=100')
        if (!feedRes.ok) return
        const logs = await feedRes.json()
        if (Array.isArray(logs) && logs.length > 0) {
          const lastTs = lastLogTimeRef.current || 0
          const newLogs = logs.filter(l => {
            const ts = new Date(l.timestamp).getTime()
            return ts > lastTs && l.text && l.text.length > 3
          }).reverse()
          if (newLogs.length > 0) {
            lastLogTimeRef.current = Math.max(...newLogs.map(l => new Date(l.timestamp).getTime()))
            setTranscript(prev => [...prev, ...newLogs.map(l => ({
              who: l.agentId || 'system',
              text: l.text,
              isLog: true,
              level: l.level
            }))])
          }
        }

        const actRes = await fetch('/api/command/activity')
        if (!actRes.ok) return
        const act = await actRes.json()
        const cmd = (act.recent || []).find(c => c.id === activeCmdId)
        if (cmd && (cmd.status === 'completed' || cmd.status === 'failed')) {
          setTranscript(prev => [...prev, {
            who: 'system',
            text: cmd.status === 'completed'
              ? '✅ Kod değişikliği tamamlandı!'
              : `❌ Kod değişikliği başarısız${cmd.error ? ': ' + cmd.error : ''}`,
            isResult: true
          }])
          setActiveCmdId(null)
          lastLogTimeRef.current = null
        }
      } catch (e) {
        console.error('Command poll error:', e)
      }
    }

    const interval = setInterval(poll, 2000)
    return () => clearInterval(interval)
  }, [activeCmdId])

  const filteredAgents = allAgents.filter(a => a.room.toLowerCase() === room)

  function speak(text, agentId) {
    return new Promise(resolve => {
      if (!window.speechSynthesis || !text || !agentId) { resolve(); return }
      try {
        const currentLang = langRef.current
        const agent = allAgents.find(a => a.id === agentId) || allAgents[0]
        const agentName = getAgentField(agent, 'name', currentLang)
        const utterance = new SpeechSynthesisUtterance(text)

        if (currentLang === 'tr') {
          utterance.lang = 'tr-TR'
          utterance.volume = 1.0
          const voices = window.speechSynthesis.getVoices()
          const femalePat = /tunali|asli|sedef|kadın|kad.n|k.z|kiz|female/i
          const malePat = /murat|erkek|male/i
          let trVoices = voices.filter(v => v.lang.startsWith('tr'))
          if (trVoices.length === 0) {
            trVoices = voices.filter(v => v.lang.startsWith('en'))
          }
          if (trVoices.length > 0) {
            let chosen
            if (agent.gender === 'female') {
              chosen = trVoices.find(v => femalePat.test(v.name))
                || trVoices.find(v => !malePat.test(v.name))
                || trVoices[0]
            } else {
              chosen = trVoices.find(v => malePat.test(v.name))
                || trVoices.find(v => !femalePat.test(v.name))
                || trVoices[0]
            }
            utterance.voice = chosen
            const isFemale = femalePat.test(chosen.name)
            const basePitch = agent.voice?.pitch ?? 1.0
            if (agent.gender === 'female' && !isFemale) {
              utterance.pitch = Math.min(basePitch * 1.8, 2)
            } else if (agent.gender === 'male' && isFemale) {
              utterance.pitch = Math.max(basePitch * 0.5, 0.1)
            } else {
              utterance.pitch = basePitch
            }
            utterance.rate = agent.voice?.rate ?? 0.9
          }
        } else {
          utterance.lang = 'en-US'
          utterance.rate = agent.voice?.rate ?? 0.9
          utterance.pitch = agent.voice?.pitch ?? 1.0
          utterance.volume = 1.0
          const voiceId = getVoiceId(agent, currentLang)
          const voices = window.speechSynthesis.getVoices()
          if (voiceId) {
            const exact = voices.find(v => v.name === voiceId)
            if (exact) { utterance.voice = exact }
            else {
              const gender = agent.gender
              const fallback = voices.find(v =>
                v.lang.startsWith('en') &&
                (gender === 'female' ? v.name.includes('Female') : v.name.includes('Male'))
              ) || voices.find(v => v.lang.startsWith('en'))
              if (fallback) utterance.voice = fallback
            }
          }
        }

        setSpeakingAgentId(agentId)
        setCaption(`[${agentName}] ${text}`)
        utterance.onend = () => { setSpeakingAgentId(null); resolve() }
        utterance.onerror = () => { setSpeakingAgentId(null); resolve() }
        window.speechSynthesis.speak(utterance)
      } catch (e) {
        console.error('Speech error:', e)
        setSpeakingAgentId(null)
        resolve()
      }
    })
  }

  async function askAI(agentId, userMessage, history) {
    try {
      const res = await fetch('/api/agent/ask', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: userMessage,
          room: room,
          agentId: agentId,
          language: langRef.current,
          history: history
        })
      })
      if (!res.ok) { console.error('AI API error:', res.status); return '' }
      const data = await res.json()
      if (data.command) {
        const c = data.command
        setActiveCmdId(c.id)
        lastLogTimeRef.current = Date.now()
        setTranscript(prev => [...prev, {
          who: agentId,
          text: `🔧 ${c.message || 'Komut kuyruğa alındı, openCLI işliyor...'}`,
          isResult: true
        }])
      }
      return data.content || ''
    } catch (e) {
      console.error('AI call failed:', e)
      return ''
    }
  }

  async function checkChime(history) {
    try {
      const res = await fetch('/api/agent/chime', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          room: room,
          language: langRef.current,
          history: history
        })
      })
      if (!res.ok) return null
      const data = await res.json()
      if (data && data.agentId && data.content) return data
      return null
    } catch { return null }
  }

  async function speakSequence(agentIds, text, userMessage) {
    speakingRef.current = true
    const localHistory = transcript.map(m => ({
      who: m.who === 'user' ? 'User' : (agentNames[m.who] || m.who),
      text: m.text
    }))

    for (const agentId of agentIds) {
      if (!speakingRef.current) break
      const response = await askAI(agentId, userMessage, localHistory)
      if (response) {
        localHistory.push({ who: agentNames[agentId] || agentId, text: response })
        setTranscript(prev => [...prev, { who: agentId, text: response }])
        await speak(response, agentId)
      }
    }

    const chime = await checkChime(localHistory)
    if (chime && speakingRef.current) {
      localHistory.push({ who: agentNames[chime.agentId] || chime.agentId, text: chime.content })
      setTranscript(prev => [...prev, { who: chime.agentId, text: chime.content }])
      await speak(chime.content, chime.agentId)
    }

    speakingRef.current = false
  }

  function processUserInput(text) {
    if (!text.trim()) return
    try {
      if (window.speechSynthesis) window.speechSynthesis.cancel()
      speakingRef.current = false
      if (timerRef.current) clearTimeout(timerRef.current)
      const lower = text.toLowerCase()

      const stopCmds = ['kes', 'sus', 'dur', 'yeter', 'kapa', 'stop', 'shut', 'yapma', 'silence']
      if (stopCmds.some(c => {
        const re = new RegExp('\\b' + c + '\\b', 'i');
        return re.test(lower) || lower.includes(c + ' ses') || lower.includes(c + ' konuş');
      })) {
        setTranscript(prev => [...prev, { who: 'user', text: lower }])
        return
      }

      const { agentIds } = routeMessage(text, room)
      setTranscript(prev => [...prev, { who: 'user', text }])
      if (agentIds.length > 0) speakSequence(agentIds, text, text)
    } catch (e) {
      console.error('process error:', e)
    }
  }

  function handleChatSend() {
    if (!chatInput.trim()) return
    processUserInput(chatInput.trim())
    setChatInput('')
  }

  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition

  function toggleListening() {
    if (!SpeechRecognition) { alert(t(lang, 'noSpeech')); return }
    if (listening) {
      recognitionRef.current?.stop()
      setListening(false)
      return
    }
    const recognition = new SpeechRecognition()
    recognition.continuous = false
    recognition.interimResults = true
    recognition.lang = lang === 'tr' ? 'tr-TR' : 'en-US'
    let finalTranscript = ''
    recognition.onresult = (event) => {
      for (const result of event.results)
        if (result.isFinal) finalTranscript += result[0].transcript + ' '
      setCaption(`🎤 ${Array.from(event.results).map(r => r[0].transcript).join(' ')}`)
    }
    recognition.onend = () => {
      setListening(false)
      if (finalTranscript.trim() && !speakingRef.current) processUserInput(finalTranscript.trim())
      else setCaption('')
    }
    recognition.onerror = () => setListening(false)
    recognitionRef.current = recognition
    recognition.start()
    setListening(true)
    setCaption(t(lang, 'captions') + '...')
  }

  const agentNames = {}
  allAgents.forEach(a => { agentNames[a.id] = getAgentField(a, 'name', lang) })

  return (
    <div className="min-h-screen bg-blue-950 text-white flex flex-col">
      <div className="flex-1 max-w-7xl mx-auto w-full px-4 py-6 flex flex-col">
        <header className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-4xl font-bold text-orange-400">
              {t(lang, 'title')}
            </h1>
            <p className="text-slate-500 text-sm mt-1">{t(lang, 'subtitle')}</p>
          </div>
          <div className="flex items-center gap-3">
            <div className="flex bg-slate-800 rounded-xl p-0.5 border border-slate-700/50">
              {languages.map(l => (
                <button key={l.code} onClick={() => setLang(l.code)}
                  className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${lang === l.code ? 'bg-violet-600 text-white' : 'text-slate-400 hover:text-white'}`}
                >{l.label}</button>
              ))}
            </div>
            <div className="flex bg-slate-800 rounded-xl p-0.5 border border-slate-700/50">
              {['meeting', 'self-improve'].map(v => (
                <button key={v} onClick={() => setView(v)}
                  className={`px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${view === v ? 'bg-violet-600 text-white' : 'text-slate-400 hover:text-white'}`}
                >{v === 'meeting' ? (lang === 'tr' ? 'Toplantı' : 'Meeting') : (lang === 'tr' ? '⚡Kendini Geliştir' : '⚡Self Improve')}</button>
              ))}
            </div>
            <div className="flex bg-slate-800 rounded-xl p-0.5 border border-slate-700/50">
              {['strategy', 'engineering'].map(r => (
                <button key={r} onClick={() => setRoom(r)}
                  className={`px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${room === r ? (r === 'strategy' ? 'bg-violet-600 text-white' : 'bg-cyan-600 text-white') : 'text-slate-400 hover:text-white'}`}
                >{r === 'strategy' ? t(lang, 'strategyRoom') : t(lang, 'engineeringRoom')}</button>
              ))}
            </div>
          </div>
        </header>

        {view === 'self-improve' ? (
          <SelfImprovementPanel lang={lang} />
        ) : (
          <>
            <AgentGrid agents={filteredAgents} speakingAgentId={speakingAgentId} lang={lang} />

            <div className="mt-3">
              <ActivityTerminal lang={lang} />
            </div>

            <div className="mt-3 bg-slate-800/40 backdrop-blur-sm rounded-2xl border border-slate-700/50 p-4 flex-1 min-h-[500px] max-h-[70vh] overflow-y-auto">
              {transcript.length === 0 ? (
                <p className="text-slate-500 text-sm text-center py-4">
                  {lang === 'tr' ? 'Merhaba! Bir şey yaz veya 🎤 butonuna bas.' : 'Hello! Type something or press 🎤.'}
                </p>
              ) : (
                <div className="space-y-2">
                  {transcript.map((m, i) => (
                    <div key={i} className="flex items-start gap-2 text-sm leading-relaxed">
                      <span className={`font-semibold flex-shrink-0 ${m.who === 'user' ? 'text-slate-400' : m.isDiff ? 'text-emerald-400' : m.isLog ? 'text-slate-500' : m.isResult ? 'text-cyan-400' : 'text-violet-400'}`}>
                        {m.who === 'user' ? (lang === 'tr' ? 'Sen' : 'You') : (m.isDiff ? (lang === 'tr' ? 'Diff' : 'Diff') : m.isLog ? '⚡' : (agentNames[m.who] || m.who))}:
                      </span>
                      <span className={`${m.isDiff ? 'text-emerald-300 whitespace-pre-wrap font-mono text-xs' : m.isLog ? (m.level === 'success' ? 'text-emerald-400' : m.level === 'error' ? 'text-red-400' : m.level === 'warn' ? 'text-amber-400' : 'text-cyan-300') : m.isResult ? 'text-cyan-300' : 'text-slate-300'}`}>
                        {m.text}
                      </span>
                    </div>
                  ))}
                  <div ref={chatEndRef} />
                </div>
              )}
            </div>

            <div className="mt-auto pt-4">
              <div className="bg-slate-800/60 backdrop-blur-sm rounded-2xl border border-slate-700/50 p-4">
                <div className="flex gap-2">
                  <input type="text" value={chatInput}
                    onChange={e => setChatInput(e.target.value)}
                    onKeyDown={e => e.key === 'Enter' && handleChatSend()}
                    placeholder={lang === 'tr' ? 'Mesaj yaz...' : 'Type a message...'}
                    className="flex-1 bg-slate-900/50 text-white rounded-xl px-4 py-2.5 text-sm outline-none border border-slate-600/50 focus:border-violet-500/50 placeholder-slate-500"
                  />
                  <button onClick={handleChatSend}
                    className="px-4 py-2.5 bg-violet-600 hover:bg-violet-500 text-white rounded-xl text-sm font-medium transition-all">
                    {lang === 'tr' ? 'Gönder' : 'Send'}
                  </button>
                  <button onClick={toggleListening}
                    className={`px-4 py-2.5 rounded-xl text-sm font-medium transition-all ${listening ? 'bg-red-600 text-white animate-pulse' : 'bg-slate-700/50 text-slate-300 hover:bg-slate-700'}`}>
                    🎤
                  </button>
                </div>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
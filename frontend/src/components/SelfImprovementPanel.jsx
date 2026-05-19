import { useState, useEffect } from 'react'

const categoryIcons = { code_quality: '🔧', performance: '⚡', agent: '🤖', system: '🖥', security: '🔒' }
const statusColors = { pending: 'text-amber-400', applied: 'text-emerald-400', dismissed: 'text-slate-500', failed: 'text-red-400' }
const statusIcons = { pending: '⏳', applied: '✅', dismissed: '❌', failed: '⚠️' }

export default function SelfImprovementPanel({ lang }) {
  const [tab, setTab] = useState('suggestions')
  const [suggestions, setSuggestions] = useState([])
  const [report, setReport] = useState(null)
  const [status, setStatus] = useState(null)
  const [scanning, setScanning] = useState(false)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetchStatus()
    fetchSuggestions()
  }, [])

  async function fetchStatus() {
    try {
      const res = await fetch('/api/self-improve/status')
      if (res.ok) setStatus(await res.json())
    } catch {}
    setLoading(false)
  }

  async function fetchSuggestions() {
    try {
      const res = await fetch('/api/self-improve/suggestions')
      if (res.ok) setSuggestions(await res.json())
    } catch {}
  }

  async function handleScan() {
    setScanning(true)
    try {
      const res = await fetch('/api/self-improve/scan', { method: 'POST' })
      if (res.ok) {
        const data = await res.json()
        setReport(data)
        setTab('report')
      }
    } catch {}
    setScanning(false)
    fetchSuggestions()
    fetchStatus()
  }

  async function handleApply(id) {
    try {
      const res = await fetch(`/api/self-improve/apply/${id}`, { method: 'POST' })
      if (res.ok) {
        fetchSuggestions()
        fetchStatus()
      }
    } catch {}
  }

  async function handleDismiss(id) {
    try {
      await fetch(`/api/self-improve/dismiss/${id}`, { method: 'POST' })
      fetchSuggestions()
    } catch {}
  }

  async function toggleAuto() {
    const newMode = !status?.autoMode
    try {
      const res = await fetch('/api/self-improve/auto', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ enabled: newMode })
      })
      if (res.ok) setStatus(prev => ({ ...prev, autoMode: newMode }))
    } catch {}
  }

  const pending = suggestions.filter(s => s.status === 'pending')
  const applied = suggestions.filter(s => s.status === 'applied')

  return (
    <div className="bg-slate-800/40 backdrop-blur-sm rounded-2xl border border-slate-700/50 p-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-bold text-white">
          {lang === 'tr' ? '🔄 Kendini Geliştirme' : '🔄 Self Improvement'}
        </h2>
        {status && (
          <div className="flex items-center gap-2 text-xs">
            <span className="text-slate-400">
              {lang === 'tr' ? 'Otomatik:' : 'Auto:'}
            </span>
            <button onClick={toggleAuto}
              className={`px-2 py-0.5 rounded-lg text-xs font-medium transition-all ${status.autoMode ? 'bg-emerald-600 text-white' : 'bg-slate-700 text-slate-400'}`}>
              {status.autoMode ? (lang === 'tr' ? 'Açık' : 'ON') : (lang === 'tr' ? 'Kapalı' : 'OFF')}
            </button>
          </div>
        )}
      </div>

      <div className="flex gap-1 mb-4">
        {['suggestions', 'report', 'performance'].map(t => (
          <button key={t} onClick={() => setTab(t)}
            className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${tab === t ? 'bg-violet-600 text-white' : 'text-slate-400 hover:text-white'}`}>
            {t === 'suggestions' ? (lang === 'tr' ? `Öneriler (${pending.length})` : `Suggestions (${pending.length})`) :
             t === 'report' ? (lang === 'tr' ? 'Rapor' : 'Report') :
             (lang === 'tr' ? 'Performans' : 'Performance')}
          </button>
        ))}
        <button onClick={handleScan} disabled={scanning}
          className="ml-auto px-3 py-1.5 rounded-lg text-xs font-medium bg-cyan-600 hover:bg-cyan-500 text-white transition-all disabled:opacity-50">
          {scanning ? (lang === 'tr' ? 'Taranıyor...' : 'Scanning...') : (lang === 'tr' ? '🔍 Tara' : '🔍 Scan')}
        </button>
      </div>

      {tab === 'suggestions' && (
        <div className="space-y-2 max-h-[400px] overflow-y-auto">
          {pending.length === 0 ? (
            <p className="text-slate-500 text-sm text-center py-8">
              {lang === 'tr' ? 'Henüz iyileştirme önerisi yok. "Tara" butonuna bas.' : 'No suggestions yet. Click "Scan".'}
            </p>
          ) : (
            pending.map(s => (
              <div key={s.id} className="bg-slate-900/40 rounded-xl p-3 border border-slate-700/30">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-1.5 mb-1">
                      <span>{categoryIcons[s.category] || '📌'}</span>
                      <span className={`text-xs font-medium ${statusColors[s.status]}`}>{statusIcons[s.status]} {s.status}</span>
                      <span className="text-[10px] text-slate-500">P{s.priority}</span>
                    </div>
                    <p className="text-sm text-white font-medium truncate">{s.title}</p>
                    <p className="text-xs text-slate-400 mt-0.5 line-clamp-2">{s.description}</p>
                    {s.targetFile && (
                      <p className="text-[10px] text-slate-500 mt-1 font-mono">{s.targetFile}</p>
                    )}
                  </div>
                  <div className="flex gap-1 flex-shrink-0">
                    <button onClick={() => handleApply(s.id)}
                      className="px-2 py-1 text-[10px] bg-emerald-600/20 text-emerald-400 hover:bg-emerald-600/30 rounded-lg transition-all">
                      {lang === 'tr' ? 'Uygula' : 'Apply'}
                    </button>
                    <button onClick={() => handleDismiss(s.id)}
                      className="px-2 py-1 text-[10px] bg-slate-700/50 text-slate-400 hover:bg-slate-700 rounded-lg transition-all">
                      ✕
                    </button>
                  </div>
                </div>
              </div>
            ))
          )}
          {applied.length > 0 && (
            <details className="mt-4">
              <summary className="text-xs text-slate-500 cursor-pointer hover:text-slate-400">
                {lang === 'tr' ? `Uygulananlar (${applied.length})` : `Applied (${applied.length})`}
              </summary>
              <div className="mt-2 space-y-1">
                {applied.map(s => (
                  <div key={s.id} className="text-xs text-slate-500 flex items-center gap-1.5">
                    <span>✅</span>
                    <span className="truncate">{s.title}</span>
                  </div>
                ))}
              </div>
            </details>
          )}
        </div>
      )}

      {tab === 'report' && (
        <div className="space-y-3">
          {!report ? (
            <p className="text-slate-500 text-sm text-center py-8">
              {lang === 'tr' ? 'Henüz tarama yapılmadı.' : 'No scan performed yet.'}
            </p>
          ) : (
            <>
              <div className="grid grid-cols-2 gap-2">
                <div className="bg-slate-900/40 rounded-xl p-3 text-center">
                  <p className="text-2xl font-bold text-white">{report.totalFiles}</p>
                  <p className="text-xs text-slate-400">{lang === 'tr' ? 'Dosya Tarandı' : 'Files Scanned'}</p>
                </div>
                <div className="bg-slate-900/40 rounded-xl p-3 text-center">
                  <p className="text-2xl font-bold text-amber-400">{report.todoCount}</p>
                  <p className="text-xs text-slate-400">TODO/FIXME</p>
                </div>
                <div className="bg-slate-900/40 rounded-xl p-3 text-center">
                  <p className="text-2xl font-bold text-cyan-400">{report.suggestionsGenerated}</p>
                  <p className="text-xs text-slate-400">{lang === 'tr' ? 'Öneri' : 'Suggestions'}</p>
                </div>
                <div className="bg-slate-900/40 rounded-xl p-3 text-center">
                  <p className="text-2xl font-bold text-violet-400">{report.hardcodedValues || 0}</p>
                  <p className="text-xs text-slate-400">{lang === 'tr' ? 'Sabit Değer' : 'Hardcoded'}</p>
                </div>
              </div>

              {report.agentPerformance && Object.keys(report.agentPerformance).length > 0 && (
                <div>
                  <p className="text-xs font-medium text-slate-400 mb-1.5">
                    {lang === 'tr' ? 'Ajan Performansı' : 'Agent Performance'}
                  </p>
                  <div className="space-y-1">
                    {Object.entries(report.agentPerformance).map(([id, rate]) => (
                      <div key={id} className="flex items-center gap-2">
                        <span className="text-xs text-slate-300 w-16">{id}</span>
                        <div className="flex-1 h-2 bg-slate-700 rounded-full overflow-hidden">
                          <div className={`h-full rounded-full transition-all ${rate >= 70 ? 'bg-emerald-500' : rate >= 40 ? 'bg-amber-500' : 'bg-red-500'}`}
                            style={{ width: `${rate}%` }} />
                        </div>
                        <span className="text-xs text-slate-400 w-10 text-right">{rate}%</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      )}

      {tab === 'performance' && <PerformanceView lang={lang} />}
    </div>
  )
}

function PerformanceView({ lang }) {
  const [stats, setStats] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch('/api/self-improve/performance')
      .then(r => r.json())
      .then(d => { setStats(d); setLoading(false) })
      .catch(() => setLoading(false))
  }, [])

  if (loading) return <p className="text-slate-500 text-sm text-center py-4">Loading...</p>

  return (
    <div className="space-y-2">
      {stats.length === 0 ? (
        <p className="text-slate-500 text-sm text-center py-4">
          {lang === 'tr' ? 'Henüz veri yok' : 'No data yet'}
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="text-slate-400 border-b border-slate-700/50">
                <th className="text-left py-1.5 pr-2">{lang === 'tr' ? 'Ajan' : 'Agent'}</th>
                <th className="text-left py-1.5 pr-2">{lang === 'tr' ? 'Oda' : 'Room'}</th>
                <th className="text-right py-1.5 pr-2">{lang === 'tr' ? 'Görev' : 'Tasks'}</th>
                <th className="text-right py-1.5 pr-2">✅</th>
                <th className="text-right py-1.5 pr-2">❌</th>
                <th className="text-right py-1.5">{lang === 'tr' ? 'Başarı' : 'Rate'}</th>
              </tr>
            </thead>
            <tbody>
              {stats.map(s => (
                <tr key={s.agentId} className="border-b border-slate-800/50 hover:bg-slate-800/30">
                  <td className="py-1.5 pr-2 text-white font-medium">{s.agentName}</td>
                  <td className="py-1.5 pr-2 text-slate-400">{s.room}</td>
                  <td className="py-1.5 pr-2 text-right text-slate-300">{s.totalTasks}</td>
                  <td className="py-1.5 pr-2 text-right text-emerald-400">{s.successCount}</td>
                  <td className="py-1.5 pr-2 text-right text-red-400">{s.failureCount}</td>
                  <td className="py-1.5 text-right">
                    <span className={`font-medium ${parseInt(s.successRate) >= 70 ? 'text-emerald-400' : parseInt(s.successRate) >= 40 ? 'text-amber-400' : 'text-red-400'}`}>
                      {s.successRate}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

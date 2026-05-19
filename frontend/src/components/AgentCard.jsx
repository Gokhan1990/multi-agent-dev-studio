import { getAgentField } from '../data/agents'

export default function AgentCard({ agent, isSpeaking, lang }) {
  const name = getAgentField(agent, 'name', lang)
  const title = getAgentField(agent, 'title', lang)
  const role = getAgentField(agent, 'role', lang)
  const personality = getAgentField(agent, 'personality', lang)
  const task = getAgentField(agent, 'task', lang)

  return (
    <div className={`
      relative flex flex-col items-center p-6 rounded-2xl transition-all duration-500
      ${isSpeaking
        ? 'scale-105 bg-slate-800/90 shadow-[0_0_40px_rgba(139,92,246,0.3)] ring-2 ring-violet-500/50'
        : 'bg-slate-800/60 hover:bg-slate-800/80 hover:scale-[1.02]'
      }
    `}>
      <div className="relative mb-4">
        <div className={`
          w-20 h-20 rounded-full flex items-center justify-center text-2xl font-bold text-white
          bg-gradient-to-br ${agent.color} shadow-lg
          transition-all duration-500
          ${isSpeaking ? 'scale-110' : ''}
        `}>
          {agent.short}
        </div>

        {isSpeaking && (
          <>
            <div className="absolute -inset-1.5 rounded-full bg-gradient-to-br from-violet-400 via-purple-500 to-fuchsia-500 opacity-40 blur-sm animate-pulse" />
            <div className="absolute -inset-0.5 rounded-full bg-gradient-to-br from-violet-400 via-purple-500 to-fuchsia-500" style={{ animation: 'spin 3s linear infinite' }} />
          </>
        )}
      </div>

      <h3 className="text-white font-semibold text-sm text-center leading-tight mb-0.5">
        {name}
      </h3>
      <p className="text-slate-300 text-xs font-medium mb-0.5">{title}</p>
      <p className="text-slate-500 text-[10px] mb-2">"{personality}"</p>

      <span className={`
        text-[10px] font-medium px-2.5 py-0.5 rounded-full mb-2.5
        ${agent.room === 'Strategy'
          ? 'bg-violet-500/20 text-violet-300'
          : 'bg-cyan-500/20 text-cyan-300'
        }
      `}>
        {role}
      </span>

      <p className="text-slate-500 text-[11px] text-center leading-relaxed line-clamp-2">
        {task}
      </p>

      {isSpeaking && (
        <div className="absolute -bottom-1 left-1/2 -translate-x-1/2">
          <span className="text-[10px] font-medium text-violet-400 bg-violet-500/10 px-3 py-0.5 rounded-full animate-pulse">
            {lang === 'tr' ? 'Konuşuyor...' : 'Speaking...'}
          </span>
        </div>
      )}
    </div>
  )
}

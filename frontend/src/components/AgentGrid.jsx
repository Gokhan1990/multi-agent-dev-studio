import AgentCard from './AgentCard'

export default function AgentGrid({ agents, speakingAgentId, lang }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 w-full">
      {agents.map(agent => (
        <AgentCard
          key={agent.id}
          agent={agent}
          isSpeaking={agent.id === speakingAgentId}
          lang={lang}
        />
      ))}
    </div>
  )
}

import { Component } from 'react'

export default class ErrorBoundary extends Component {
  constructor(props) {
    super(props)
    this.state = { error: null }
  }
  static getDerivedStateFromError(error) {
    return { error }
  }
  componentDidCatch(error, info) {
    console.error('ErrorBoundary caught:', error, info)
  }
  render() {
    if (this.state.error) {
      return (
        <div className="min-h-screen bg-slate-900 flex items-center justify-center p-8">
          <div className="bg-slate-800/80 rounded-2xl p-8 max-w-md border border-slate-700/50">
            <h1 className="text-red-400 text-lg font-bold mb-2">Something went wrong</h1>
            <p className="text-slate-400 text-sm mb-4">An unexpected error occurred. Please refresh the page.</p>
            <pre className="text-xs text-slate-500 bg-slate-900/50 p-3 rounded-xl overflow-auto max-h-32">
              {this.state.error.message}
            </pre>
            <button onClick={() => window.location.reload()}
              className="mt-4 bg-violet-600 hover:bg-violet-500 text-white px-4 py-2 rounded-xl text-sm"
            >Refresh Page</button>
          </div>
        </div>
      )
    }
    return this.props.children
  }
}

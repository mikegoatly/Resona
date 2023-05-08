import logo from './Resona.png'
import './App.css'
import { useQuery } from '@tanstack/react-query'

function App() {
    const { isLoading, error, data } = useQuery({
        queryFn: () =>
            fetch('/api').then(
                (res) => res.text(),
            ),
    })

    return (
        <div className="App">
            <header className="App-header">
                <img src={logo} className="App-logo" alt="logo" />
                <p>
                    { isLoading ? "Loading..." : "Last played: " + data }
                </p>
            </header>
        </div>
    )
}

export default App

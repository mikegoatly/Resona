import { BrowserRouter, Route, Routes, } from 'react-router-dom'
import LibraryView from './pages/LibraryView'
import { useState } from 'react'
import Navigation from './components/Navigation';
import PageContainer from './components/PageContainer'

function App() {
    const [isOpen, setIsOpen] = useState(false);

    const toggle = () => setIsOpen(!isOpen);

    return (
        <BrowserRouter>
            <div id="root">
                <Navigation />

                <PageContainer>
                    <Routes>
                        <Route path="/library/:audioKind" Component={LibraryView} />
                    </Routes>
                </PageContainer>
            </div>
        </BrowserRouter>
    )
}

export default App

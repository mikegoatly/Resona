import { BrowserRouter, Route, Routes, } from 'react-router-dom'
import LibraryView from './pages/LibraryView'
import { useState } from 'react'
import Navigation from './components/Navigation';
import PageContainer from './components/PageContainer'
import UploadView from './pages/UploadView';

function App() {
    const [isOpen, setIsOpen] = useState(false);

    const toggle = () => setIsOpen(!isOpen);

    return (
        <BrowserRouter>
            <div id="main">
                <Navigation />

                <PageContainer>
                    <Routes>
                        <Route path="/library/:audioKind/add" Component={UploadView} />
                        <Route path="/library/:audioKind" Component={LibraryView} />
                    </Routes>
                </PageContainer>
            </div>
        </BrowserRouter>
    )
}

export default App

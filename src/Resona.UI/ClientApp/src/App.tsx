import { BrowserRouter, Route, Routes, } from 'react-router-dom'
import LibraryView from './pages/LibraryView'
import Navigation from './components/Navigation';
import PageContainer from './components/PageContainer'
import UploadView from './pages/UploadView';
import { UploadContext, createUploadContext } from './components/UploadQueue/UploadContext';

function App() {
    const uploadContext = createUploadContext();

    return (
        <BrowserRouter>
            <UploadContext.Provider value={uploadContext}>
                <div id="main">
                    <Navigation />

                    <PageContainer>
                        <Routes>
                            <Route path="/library/:audioKind/add" Component={UploadView} />
                            <Route path="/library/:audioKind" Component={LibraryView} />
                        </Routes>
                    </PageContainer>
                </div>
            </UploadContext.Provider>
        </BrowserRouter >

    )
}

export default App

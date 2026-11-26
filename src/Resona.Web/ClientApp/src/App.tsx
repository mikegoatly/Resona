import { BrowserRouter, Route, Routes, } from 'react-router'
import LibraryView from './pages/LibraryView'
import Navigation from './components/Navigation';
import PageContainer from './components/PageContainer'
import UploadView from './pages/UploadView';
import { UploadContext, createUploadContext } from './components/UploadQueue/UploadContext';
import SpotifyView from './pages/SpotifyView';
import AudioKindManagementView from './pages/AudioKindManagementView';

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
                            <Route path="/spotify" Component={SpotifyView} />
                            <Route path="/"Component={AudioKindManagementView} />
                        </Routes>
                    </PageContainer>
                </div>
            </UploadContext.Provider>
        </BrowserRouter >

    )
}

export default App

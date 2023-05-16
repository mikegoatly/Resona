import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import Spinner from '../components/Spinner';
import Card from '@mui/material/Card';
import Typography from '@mui/material/Typography';
import './LibraryView.scss'
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Fab from '@mui/material/Fab';
import AddIcon from '@mui/icons-material/Add';

interface LibraryEntry {
    id: number;
    name: string;
    artist: string;
}

const LibraryView = () => {
    const { audioKind } = useParams()
    const navigate = useNavigate();
    if (audioKind == null) {
        navigate("/", { replace: true });
    }

    const { isLoading, data } = useQuery({
        queryKey: ['library', audioKind],
        queryFn: () => fetch(`/api/library/${audioKind}`).then(
            res => {
                if (res.ok) {
                    return res.json() as Promise<LibraryEntry[]>
                } else {
                    throw new Error("Failed to load library");
                }
            })
    });

    if (isLoading) {
        return (<Spinner />)
    }

    if (data == null || data.length === 0) {
        return (<p>Nothing in this part of the library</p>)
    }

    return (<div className="library-view">
        <section className="library-items">
            {data.map((entry) => (
                <Card variant="elevation" className="library-entry" key={entry.id}>
                    <img loading="lazy" title={`Album art for ${entry.name}`} width={50} height={50} src={`/api/library/${entry.id}/image`} />
                    <Typography variant='body1'> {entry.name}</Typography>
                </Card>
            ))}
        </section>
        <AppBar color="primary" className="library-actions" >
            <Toolbar>
                <Fab className="add" color="secondary" aria-label="add">
                    <AddIcon onClick={() => navigate(`/library/${audioKind}/add`)} />
                </Fab>
            </Toolbar>
        </AppBar>
    </div>)
}

export default LibraryView;
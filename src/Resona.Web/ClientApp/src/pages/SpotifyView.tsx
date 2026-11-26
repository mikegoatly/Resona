import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import FormControlLabel from '@mui/material/FormControlLabel';
import IconButton from '@mui/material/IconButton';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import DeleteIcon from '@mui/icons-material/Delete';
import Spinner from '../components/Spinner';
import './SpotifyView.scss';

interface SpotifyStatus {
    enabled: boolean;
    clientId: string | null;
    deviceName: string;
    usesConnectDevice: boolean;
    connected: boolean;
    redirectUri: string;
    deviceVisible: boolean | null;
    devices: string[];
}

interface SpotifyItem {
    uri: string;
    kind: string;
    name: string;
    owner: string | null;
    imageUrl: string | null;
    hasLocalImage: boolean;
    order: number;
}

const errorMessage = (error: unknown): string => {
    if (axios.isAxiosError(error)) {
        return error.response?.data?.error ?? error.message;
    }
    return error instanceof Error ? error.message : String(error);
};

const SetupCard = ({ status }: { status: SpotifyStatus }) => {
    const queryClient = useQueryClient();
    const [enabled, setEnabled] = useState(status.enabled);
    const [clientId, setClientId] = useState(status.clientId ?? '');
    const [deviceName, setDeviceName] = useState(status.deviceName);
    const [pastedUrl, setPastedUrl] = useState('');

    const setStatus = (newStatus: SpotifyStatus) => queryClient.setQueryData(['spotify', 'status'], newStatus);

    const save = useMutation({
        mutationFn: () => axios.put<SpotifyStatus>('/api/spotify/settings', { enabled, clientId, deviceName }).then(r => r.data),
        onSuccess: setStatus,
    });

    const connect = useMutation({
        mutationFn: () => axios.post<{ authorizeUrl: string }>('/api/spotify/auth/start').then(r => r.data),
        onSuccess: data => { window.open(data.authorizeUrl, '_blank', 'noopener'); },
    });

    const finish = useMutation({
        mutationFn: () => axios.post<SpotifyStatus>('/api/spotify/auth/complete', { redirectedUrl: pastedUrl }).then(r => r.data),
        onSuccess: data => { setPastedUrl(''); setStatus(data); },
    });

    const disconnect = useMutation({
        mutationFn: () => axios.post<SpotifyStatus>('/api/spotify/auth/disconnect').then(r => r.data),
        onSuccess: setStatus,
    });

    const error = save.error ?? connect.error ?? finish.error ?? disconnect.error;

    return (
        <Card>
            <CardContent>
                <div className="row">
                    <Typography variant="h6">Spotify</Typography>
                    <Chip
                        label={status.connected ? 'Connected' : 'Not connected'}
                        color={status.connected ? 'success' : 'default'} />
                </div>

                <div className="setup-fields">
                    <FormControlLabel
                        control={<Switch checked={enabled} onChange={e => setEnabled(e.target.checked)} />}
                        label="Enable Spotify" />
                    <TextField label="Client ID" value={clientId} onChange={e => setClientId(e.target.value)}
                        helperText={`Create an app at developer.spotify.com and add ${status.redirectUri} as a redirect URI.`} />
                    {status.usesConnectDevice ? (
                        <TextField label="Device name" value={deviceName} onChange={e => setDeviceName(e.target.value)}
                            helperText="The Spotify Connect device name Resona plays through." />
                    ) : (
                        <Typography variant="body2">
                            This device plays through its own Spotify app &mdash; make sure Spotify is installed and signed in on it.
                        </Typography>
                    )}
                    <div className="row">
                        <Button variant="contained" onClick={() => save.mutate()} disabled={save.isPending}>Save</Button>
                    </div>
                </div>

                {error && <Alert severity="error">{errorMessage(error)}</Alert>}

                {!status.connected && (
                    <div className="setup-fields">
                        <div className="row">
                            <Button variant="contained" color="secondary" onClick={() => connect.mutate()} disabled={connect.isPending}>
                                Connect Spotify
                            </Button>
                        </div>
                        <Typography variant="body2">
                            After approving, your browser will show an error page at 127.0.0.1 &mdash; copy the full address from the address bar and paste it below.
                        </Typography>
                        <TextField label="Redirected address" value={pastedUrl} onChange={e => setPastedUrl(e.target.value)} />
                        <div className="row">
                            <Button variant="contained" onClick={() => finish.mutate()} disabled={finish.isPending || pastedUrl.trim() === ''}>
                                Finish connecting
                            </Button>
                        </div>
                    </div>
                )}

                {status.connected && (
                    <div className="setup-fields">
                        {status.deviceVisible === true && <Alert severity="success">{status.deviceName} is online</Alert>}
                        {status.deviceVisible === false && (
                            <Alert severity="warning">
                                {status.deviceName} is not visible &mdash; start the device or cast to it once from the Spotify app.
                            </Alert>
                        )}
                        {status.usesConnectDevice && status.devices.length > 0 && (
                            <Typography variant="body2">Visible devices: {status.devices.join(', ')}</Typography>
                        )}
                        <div className="row">
                            <Button variant="outlined" color="error" onClick={() => disconnect.mutate()} disabled={disconnect.isPending}>
                                Disconnect
                            </Button>
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
};

const CuratedList = () => {
    const queryClient = useQueryClient();
    const [url, setUrl] = useState('');

    const { data: items } = useQuery({
        queryKey: ['spotify', 'items'],
        queryFn: () => axios.get<SpotifyItem[]>('/api/spotify/items').then(r => r.data),
    });

    const setItems = (newItems: SpotifyItem[]) => queryClient.setQueryData(['spotify', 'items'], newItems);

    const add = useMutation({
        mutationFn: () => axios.post<SpotifyItem>('/api/spotify/items', { url }),
        onSuccess: () => { setUrl(''); return queryClient.invalidateQueries({ queryKey: ['spotify', 'items'] }); },
    });

    const remove = useMutation({
        mutationFn: (uri: string) => axios.delete(`/api/spotify/items/${encodeURIComponent(uri)}`),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['spotify', 'items'] }),
    });

    const reorder = useMutation({
        mutationFn: (uris: string[]) => axios.put<SpotifyItem[]>('/api/spotify/items/order', { uris }).then(r => r.data),
        onSuccess: setItems,
    });

    const move = (index: number, delta: number) => {
        if (items == null) {
            return;
        }
        const uris = items.map(i => i.uri);
        const target = index + delta;
        [uris[index], uris[target]] = [uris[target], uris[index]];
        reorder.mutate(uris);
    };

    const error = add.error ?? remove.error ?? reorder.error;

    return (
        <Card>
            <CardContent>
                <Typography variant="h6">Playlists and albums</Typography>
                <div className="setup-fields">
                    <div className="row">
                        <TextField label="Spotify share link" value={url} onChange={e => setUrl(e.target.value)} sx={{ flexGrow: 1 }} />
                        <Button variant="contained" onClick={() => add.mutate()} disabled={add.isPending || url.trim() === ''}>Add</Button>
                    </div>
                    {error && <Alert severity="error">{errorMessage(error)}</Alert>}
                </div>

                {items == null || items.length === 0
                    ? <Typography variant="body1">Nothing here yet - paste a playlist or album link above.</Typography>
                    : items.map((item, index) => (
                        <Card variant="elevation" className="spotify-entry" key={item.uri}>
                            {item.hasLocalImage
                                ? <img loading="lazy" width={50} height={50} alt={`Cover for ${item.name}`} src={`/api/spotify/items/${encodeURIComponent(item.uri)}/image`} />
                                : <div style={{ width: 50, height: 50 }} />}
                            <div className="details">
                                <Typography variant="body1">{item.name}</Typography>
                                <Typography variant="body2">{item.kind}{item.owner ? ` · ${item.owner}` : ''}</Typography>
                            </div>
                            <IconButton aria-label="Move up" disabled={index === 0 || reorder.isPending} onClick={() => move(index, -1)}><ArrowUpwardIcon /></IconButton>
                            <IconButton aria-label="Move down" disabled={index === items.length - 1 || reorder.isPending} onClick={() => move(index, 1)}><ArrowDownwardIcon /></IconButton>
                            <IconButton aria-label="Remove" onClick={() => remove.mutate(item.uri)}><DeleteIcon /></IconButton>
                        </Card>
                    ))}
            </CardContent>
        </Card>
    );
};

const SpotifyView = () => {
    const { isPending, data } = useQuery({
        queryKey: ['spotify', 'status'],
        queryFn: () => axios.get<SpotifyStatus>('/api/spotify/status').then(r => r.data),
    });

    if (isPending || data == null) {
        return <Spinner />;
    }

    return (
        <div className="spotify-view">
            <SetupCard status={data} />
            <CuratedList />
        </div>
    );
};

export default SpotifyView;

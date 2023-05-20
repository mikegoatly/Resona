import { useEffect, useState, useCallback, useLayoutEffect } from 'react';
import axios, { AxiosProgressEvent, AxiosRequestConfig } from 'axios';
import { useUploadContext } from './UploadContext';
import UploadItem from './UploadItem';
import Typography from '@mui/material/Typography';
import LinearProgress from '@mui/material/LinearProgress';
import './UploadQueue.scss'

const UploadQueue = () => {
    const { uploadQueue, dequeueUploadFile, clearQueue } = useUploadContext();
    const [currentUpload, setCurrentUpload] = useState<UploadItem | null>(null);
    const [error, setError] = useState<string>();

    const handleError = useCallback((error: any) => {
        clearQueue();
        setCurrentUpload(null);
        setError("Upload failed");
    }, [clearQueue]);

    useLayoutEffect(() => {
        if (uploadQueue.length === 0 || currentUpload) return;

        const uploadItem = uploadQueue[0];
        setCurrentUpload({ ...uploadItem, progress: 0 });

        const config = {
            onUploadProgress: (event: AxiosProgressEvent) => {
                const progress = event.progress;
                if (progress) {
                    setCurrentUpload({
                        ...uploadItem,
                        progress: progress * 100
                    });
                }
            }
        };

        const formData = new FormData();
        formData.append('file', uploadItem.file);
        formData.append('albumName', uploadItem.albumName);

        axios.post(`/api/library/${uploadItem.audioKind}`, formData, config)
            .then(() => {
                dequeueUploadFile(); // Remove the uploaded file from the queue
                setCurrentUpload(null);
            })
            .catch(handleError);
    }, [uploadQueue, currentUpload]);

    if (currentUpload == null && error == null) {
        return null;
    }

    return (<div className="upload-state">
        {error && (<Typography color="error">{error}</Typography>)}
        {currentUpload && (
            <>
                <div>Uploading {currentUpload.file.name}</div>
                <div>({uploadQueue.length} remaining)</div>
                <LinearProgress variant="determinate" value={currentUpload.progress} />
            </>
        )}
    </div>)
};

export default UploadQueue;
import { createContext, useCallback, useContext, useState } from "react";
import UploadItem from "./UploadItem";

interface UploadContextType {
    uploadQueue: UploadItem[];
    enqueueUploadFiles: (files: File[], albumName: string, audioKind: string) => void;
    dequeueUploadFile: () => void;
    clearQueue: () => void;
}

export const UploadContext = createContext<UploadContextType | undefined>(undefined);

export const useUploadContext = () => {
    const context = useContext(UploadContext);
    if (context === undefined) {
        throw new Error("useUploadContext must be used within a UploadContext.Provider");
    }
    return context;
};

export function createUploadContext() {
    const [queue, setQueue] = useState<UploadItem[]>([]);

    const enqueue = useCallback((files: File[], albumName: string, audioKind: string) => {
        const newItems: UploadItem[] = files.map(file => ({ file, progress: 0, albumName, audioKind }));
        setQueue(prevQueue => [...prevQueue, ...newItems]);
    }, []);

    const dequeue = useCallback(() => {
        setQueue(prevQueue => prevQueue.slice(1));
    }, []);

    const clear = useCallback(() => { setQueue([]) }, []);

    return {
        uploadQueue: queue,
        enqueueUploadFiles: enqueue,
        dequeueUploadFile: dequeue,
        clearQueue: clear
    };
}
import { useQuery } from '@tanstack/react-query';
import axios from 'axios';
import * as React from 'react';
import './AudioKindManagementView.scss';

interface AudioKindDetails {
    audioKind: string;
    hasCustomLibraryIcon: boolean
}

const AudioKindManagementView = () => {
    const { isLoading, data } = useQuery({
        queryKey: [ "library" ],
        queryFn: () => fetch(`/api/library`).then(
            res => {
                if (res.ok) {
                    return res.json() as Promise<AudioKindDetails[]>
                } else {
                    throw new Error("Failed to load audio kind details");
                }
            })
    });

    const onRemoveCustomIcon = async (audioKind: string) => {
        await fetch(`/api/library/${audioKind}/image`, { method: "DELETE" });

        // Force a page reload
        window.location.reload();
    }

    const onSelectUploadImage = (audioKind: string) => {
        document.getElementById(`file${audioKind}`)?.click();
    }

    const onUploadCustomIcon = async (audioKind: string, file?: File) => {
        if (file == null) {
            return;
        }

        const formData = new FormData();
        formData.append("file", file);

        await axios.post(`/api/library/${audioKind}/image`, formData);

        // Force a page reload
        window.location.reload();
    }

    return (
        <section className="audio-kind-management">
            {
                data?.map((audioKindDetail) => (
                    <div className="library-kind" key={audioKindDetail.audioKind}>
                        <img loading="lazy" title={`Icon image for ${audioKindDetail.audioKind}`} width={50} height={50} src={`/api/library/${audioKindDetail.audioKind}/image`} />

                        <span>{audioKindDetail.audioKind}</span>

                        <input
                            placeholder="Select files..."
                            type="file"
                            id={`file${audioKindDetail.audioKind}`}
                            name={`file${audioKindDetail.audioKind}`}
                            onChange={(e) => onUploadCustomIcon(audioKindDetail.audioKind, e.target.files?.[0] as File)}
                        />
                        <button onClick={() => onSelectUploadImage(audioKindDetail.audioKind)}>Upload custom icon</button>
                        {audioKindDetail.hasCustomLibraryIcon && <button onClick={() => onRemoveCustomIcon(audioKindDetail.audioKind)}>Remove custom icon</button>}
                    </div>
                )
                )}
        </section>
    )
}

export default AudioKindManagementView;
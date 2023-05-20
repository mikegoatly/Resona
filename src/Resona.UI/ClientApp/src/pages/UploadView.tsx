import Button from "@mui/material/Button";
import Typography from "@mui/material/Typography";
import { useRef, useState } from "react";
import MP3Tag from "mp3tag.js";
import { useParams } from "react-router-dom";
import "./UploadView.scss"
import { useUploadContext } from "../components/UploadQueue/UploadContext";
import { Fab } from "@mui/material";
import DeleteIcon from '@mui/icons-material/Delete';
import UploadIcon from '@mui/icons-material/UploadFile';

interface AlbumFiles {
    files: File[];
    albumName: string;
}

const UploadView = () => {
    const [state, setState] = useState<AlbumFiles[]>([]);
    const { audioKind } = useParams()
    const { enqueueUploadFiles } = useUploadContext();

    const ref = useRef<HTMLInputElement>(null);

    const albumTitle = audioKind === "audiobook" ? "Audiobook" : audioKind === "music" ? "Album" : "Sleep track";
    const audioKindText = audioKind === "audiobook" ? "audiobook" : audioKind === "music" ? "album" : "sleep track";

    async function filesChanged(files: FileList | null): Promise<void> {
        if (files == null || files.length === 0) {
            return;
        }
        else {
            const fileArray: File[] = [];
            let title: undefined | string = undefined;
            for (let i = 0; i < files.length; i++) {
                const file = files[i];

                if (file.type === "audio/mpeg" && title == null) {
                    var mp3tag = new MP3Tag(await file.arrayBuffer())
                    mp3tag.read();

                    title = mp3tag.tags.album;
                }

                fileArray.push(file);
            }

            setState(s => [
                ...s, {
                    files: fileArray,
                    albumName: title ?? "New upload"
                }]);

            if (ref.current != null) {
                ref.current.value = "";
            }
        }
    }

    const uploadFiles = () => {
        if (state == null || audioKind == null) {
            return;
        }

        for (let albumFiles of state) {
            enqueueUploadFiles(albumFiles.files, albumFiles.albumName, audioKind);
        }

        setState([]);
    }

    return (
        <div className="upload-view">
            <Typography variant="body1">
                Select the files that you want to upload. These should all be for the same {audioKindText}.
                You can upload MP3 files, and an image file called image.jpeg or image.png.
            </Typography>

            <Button className="pick-files" variant="contained" onClick={() => ref.current?.click()}>Select files...</Button>

            <input
                placeholder="Select files..."
                type="file"
                id="file"
                name="file"
                multiple
                onChange={e => filesChanged(e.target.files)}
                ref={ref}
            />

            {state != null &&
                (
                    <div className="upload-container">
                        <div className="">
                            {state.map((albumFile, index) => (
                                <div key={index} className="upload-controls">
                                    <label htmlFor="album-title">{albumTitle}</label>
                                    <input
                                        title={albumTitle}
                                        type="text"
                                        value={albumFile.albumName}
                                        onChange={e => setState(s => {
                                            const updated = [...s];
                                            updated[index] = { ...s[index], albumName: e.target.value };
                                            return updated;
                                        }
                                        )}
                                    />
                                    <Typography>
                                        {albumFile.files.length} {albumFile.files.length === 1 ? "file" : "files"}
                                    </Typography>
                                    <Fab color="secondary" aria-label="delete" onClick={() => setState(s => s.filter((_, i) => i !== index))}>
                                        <DeleteIcon />
                                    </Fab>
                                </div>
                            ))}
                        </div>

                        {state.length > 0 && (
                            <Button className="upload" variant="contained" onClick={uploadFiles}>
                                <UploadIcon />
                                <Typography>
                                    Upload
                                </Typography>
                            </Button>
                        )}
                    </div>
                )}


        </div>
    )
}

export default UploadView;
import Button from "@mui/material/Button";
import Typography from "@mui/material/Typography";
import { useRef, useState } from "react";
import MP3Tag from "mp3tag.js";
import { useParams } from "react-router-dom";
import "./UploadView.scss"
import Paper from "@mui/material/Paper";
import AutoSizer from "react-virtualized-auto-sizer";

interface State {
    files: {
        file: File;
        progress: number;
    }[]
    albumName: string;
}

const UploadView = () => {
    const [state, setState] = useState<State>();
    const { audioKind } = useParams()

    const ref = useRef<HTMLInputElement>(null);

    const albumTitle = audioKind === "audiobook" ? "Audiobook name" : audioKind === "music" ? "Album title" : "Sleep track title";
    const audioKindText = audioKind === "audiobook" ? "audiobook" : audioKind === "music" ? "album" : "sleep track";

    async function filesChanged(files: FileList | null): Promise<void> {
        if (files == null) {
            setState(undefined);
        }
        else {
            const fileArray = [];
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

            setState({
                files: fileArray.map(file => ({ file, progress: 0 })),
                albumName: title ?? "New upload"
            });
        }
    }

    // Upload the files to the server using react-query, one at a time. Progress is reported for each file.


    return (
        <div className="upload-view">
            <Typography variant="body1">
                Select the files that you want to upload. These should all be for the same {audioKindText}.
                You can upload MP3 files, and an image file called image.jpeg or image.png.
            </Typography>

            <Button className="pick-files" variant="contained" onClick={() => ref.current?.click()}>Select files...</Button>

            <input
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
                        <div className="upload-controls">
                            <label htmlFor="album-title">{albumTitle}</label>
                            <input
                                title={albumTitle}
                                type="text"
                                value={state.albumName}
                                onChange={e => setState({ ...state, albumName: e.target.value })}
                            />
                            <Button className="upload" variant="contained" onClick={uploadFiles}>Upload</Button>
                        </div>
                        <AutoSizer>
                            {({ height, width }) => (
                                <Paper className="file-progress" style={{ height, width }}>

                                    <ul>
                                        {
                                            state.files.map(({ f: { name } }) => (
                                                <li key={name}>{name}</li>
                                            ))
                                        }
                                    </ul>
                                </Paper>
                            )}
                        </AutoSizer>
                    </div>
                )}


        </div>
    )
}

export default UploadView;
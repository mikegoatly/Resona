declare module 'mp3tag.js' {
    export default class MP3Tag {
        constructor(buffer: ArrayBuffer, verbose?: boolean);

        static readBuffer(buffer: ArrayBuffer, options?: any, verbose?: boolean): any;

        read(options?: any): any;

        static writeBuffer(buffer: ArrayBuffer, tags: any, options?: any, verbose?: boolean): ArrayBuffer;

        save(options?: any): ArrayBuffer;

        remove(): boolean;

        static getAudioBuffer(buffer: ArrayBuffer): ArrayBuffer;

        getAudio(): ArrayBuffer;

        name: string;
        version: string;
        verbose: boolean;
        buffer: ArrayBuffer;
        tags: any;
        error: string;
    }
}
export type Track = {
  id: string;
  title: string;
  artist: string;
  album: string;
  durationMs: number | null;
  sizeBytes: number;
  etag: string;
};

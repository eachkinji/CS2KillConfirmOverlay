# Battlefield 6 audio source

Original OGG files from [MinecraftGD656/gd656killicon](https://github.com/MinecraftGD656/gd656killicon), commit `10d367eb6ddb158d2f5e3e250d2cf825d9b95a12`, under `src/main/resources/assets/gd656killicon/sounds/`. The upstream README declares the project MIT licensed; see the accompanying Battlefield 6 provenance/third-party notices.

The service's existing decoder supports WAV, MP3 and MP4, so the runtime copies in `../soundpacks/battlefield6/` are decoded PCM signed 16-bit WAV, preserving the source channel count and sample rate. Conversion uses the project's bundled FFmpeg:

`ffmpeg -nostdin -hide_banner -loglevel error -i SOURCE.ogg -map_metadata -1 -c:a pcm_s16le OUTPUT.wav`

| Original | Runtime file | Use |
| --- | --- | --- |
| `killsound_bf6.ogg` | `normal.wav` | Normal kill, with fallback for knife/grenade |
| `headshotkillsound_bf6.ogg` | `headshot.wav` | Headshot at every streak level |
| `assistsound_bf6.ogg` | `assist.wav` | Assist |
| `hitsound_bf6.ogg` | `hit.wav` | Retained source hit cue; current event pipeline does not generate hit events |

The original OGG directory is excluded from runtime sound-pack staging. No objective, vehicle or round cue is invented: unconfigured events remain silent, while imported Battlefield 6 manifests can supply explicit slots for the application's existing objective/round events.

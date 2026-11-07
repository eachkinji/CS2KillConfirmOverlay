# Icon-pack GPU memory audit

Run the built compatibility executable on Windows, with one GPU test process at a time:

```powershell
& '.\CompatibilityHost\bin\Release\net8.0-windows10.0.19041.0\win-x64\KillConfirmCompatibility.exe' `
  --audit-pack-memory '.\Output\pack-memory-test' '.\Widget\Assets' `
  '.\Output\cases.json' 'C:\path\to\read-only-source-profile'
```

The last argument is optional. Only its `pack-catalog.json` is copied into the
output's `isolated-profile`. User preferences are never loaded or written.
Resource folders may be referenced read-only through settings or catalog paths.
No game, service connection, live overlay window, or audio playback is started.

Cases can be a single-element/batch array, or an options object:

```json
{
  "cases": [{ "style": "battlefield4", "key": "bf4", "name": "Battlefield 4" }],
  "settings": { "ResourceLibrary.Root": "F:\\KFC素材" },
  "settleMs": 350,
  "renderMs": 400,
  "killCounts": [1, 3, 6],
  "samples": 3,
  "sampleIntervalMs": 100,
  "collectAfterRelease": true,
  "renderWidth": 640,
  "renderHeight": 360
}
```

Each case may also contain scalar `settings` overrides. Game styles are the
`GameStyleMode` names, case-insensitive. Selecting a missing/unrecognized pack
is recorded as a failure rather than silently reporting a fallback as that pack.
For a faster full inventory sweep use one sample, one kill count, and shorter
settling/rendering periods; rerun outliers individually with the defaults.

The harness uses production `FeedbackPresenter.ApplyConfigurationAsync`, its
four animation layers, `HandleKillEvent` and `DrawDesktopFrame`. Phases are:

1. `baseline`: the selected style with icons set to `none`.
2. `preloaded`: selecting the requested pack and awaiting the production preload.
3. `rendered-N`: rendering each requested kill count offscreen.
4. `released`: selecting `none` through the same production release path.
5. `released-gc`: optional explicit collection/finalization after release.

`results.csv` and `results.json` separate actual current-process GPU counters
from CPU private bytes / working set. `gpu_local_bytes` and
`gpu_nonlocal_bytes` are median samples from the renderer's own adapter, node 0,
using `IDXGIAdapter3.QueryVideoMemoryInfo`. The probe obtains that exact adapter
through the existing `CanvasDevice`'s `IDirect3DDxgiInterfaceAccess`, then
`IDXGIDevice.GetAdapter`. It does **not** sum factory-enumerated adapters: virtual
display drivers can expose multiple adapter LUIDs for the same physical GPU and
duplicate its process counters. Raw per-adapter samples and budgets are in JSON,
with `IsRendererAdapter` and `RendererAdapterLuid` identifying the authoritative
sample. Query failures remain null and carry errors; they are not zero.
`phase.json` includes PID and stage for an external sampler. `run.json` records
settings, timings, dimensions and methodology; interrupted runs retain completed
CSV rows and the last per-pack JSON. `complete.txt` signals the sweep finished.
Progress readers should open `phase.json` with `FileShare.ReadWrite |
FileShare.Delete` so atomic replacement remains possible. The writer retries
temporary I/O or permission failures up to eight times with 560 ms total delay;
persistent failures are still reported, not silently swallowed.

`textures/*.json` inventories reachable cached `CanvasBitmap` and
`CanvasRenderTarget` objects, deduplicated by object identity, with dimensions
and **width × height × 4 estimates**. These are logical decoded image estimates,
not driver allocation sizes or actual VRAM. Actual counters include device,
driver, font, effect, alignment and offscreen-render-target overhead. WPF/DWM
presentation surfaces and a separate running UWP host are not measured.

On a discrete GPU local memory normally corresponds to dedicated VRAM; non-local
is shared system memory. UMA adapters have different semantics. Compare each
pack against its `none` baseline, and distinguish retained driver caches from
retained application textures. Use fresh-process single-case runs to confirm
outliers and avoid interpreting batch allocator retention as a leak.

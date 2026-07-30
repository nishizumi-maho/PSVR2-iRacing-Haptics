# PSVR2 Toolkit analysis

## Scope

- repository: `BnuuySolutions/PSVR2Toolkit`;
- branch: `main`;
- reviewed commit: `9e24e6ef475660481e8b46366aaa3cb24d0b4fde`;
- commit date: July 29, 2026;
- version defined in `projects/common/config.h`: driver `0.2.1`, branch
  determined by build configuration;
- no change was made to the Toolkit repository.

## Discovery and loading

`CustomShareManager::setupCAPIPath()` is called by
`projects/psvr2_openvr_driver_ex/device_provider_proxy.cpp`. On Windows it
discovers the directory containing the manager's DLL and writes that directory
to:

```text
%TEMP%\psvr2tk_capi_path.txt
```

The official loader in
`projects/psvr2_toolkit_capi_loader/psvr2tk_capi_loader.cpp`:

1. reads only the first line;
2. appends `psvr2_toolkit_capi.dll`;
3. calls `LoadLibraryExA` with `LOAD_WITH_ALTERED_SEARCH_PATH` on Windows;
4. resolves exports with `GetProcAddress`.

The C# app follows the same discovery path, loads the absolute DLL path with
`NativeLibrary.Load`, resolves functions with `NativeLibrary.GetExport`, and
does not depend on `PATH`, the registry or `System32`.

## Signatures and ABI

The header `projects/psvr2_toolkit_capi/psvr2tk_capi.h` declares:

```cpp
int  psvr2_toolkit_init();
void psvr2_toolkit_deinit();
bool psvr2_toolkit_get_driver_active();
void psvr2_toolkit_set_hmd_rumble(uint8_t rumbleHz);
```

In the .NET client:

- `int` maps to `Int32`;
- `uint8_t` maps to `byte`;
- C++ `bool` is marshalled as `UnmanagedType.I1`;
- all exports are `extern "C"`;
- Cdecl is declared; the Windows x64 ABI uses a unified calling convention.

No C++ bridge library was required.

## Initialization and slots

`psvr2_toolkit_init()` creates the sharing singleton, checks the mutex
representing an active driver and attempts to acquire a client slot.

| Code | Constant | Meaning |
| ---: | --- | --- |
| 0 | `PSVR2TK_RESULT_OK` | initialized |
| -1 | `PSVR2TK_RESULT_DRIVER_INACTIVE` | driver inactive |
| -2 | `PSVR2TK_RESULT_NO_SLOT` | no free slot |

`projects/libcustomshare/custom_share_manager.h` defines `k_maxSlots = 8`.
`psvr2_toolkit_deinit()` releases the slot but does not send a rumble-OFF
command.

## Rumble command path

`psvr2_toolkit_set_hmd_rumble`:

1. creates a `DriverCommand`;
2. sets `type = DriverCommandType::HeadsetRumbleSet`;
3. stores one `uint8_t rumbleHz`;
4. calls `CustomShareManager::submitCommand`.

The command enters a 256-entry shared ring buffer. The driver thread in
`projects/psvr2_openvr_driver_ex/command_thread.cpp` consumes commands and
handles `HeadsetRumbleSet` by calling:

```cpp
ControlCommand(true, 0x08, &rumbleHz, 1, 0, 0, 1);
```

PSVR2 Toolkit therefore remains responsible for IPC, the driver thread and the
USB control command. This app is only an external C API client.

## Proven and unproven limits

### Range

The API accepts `uint8_t` and applies no clamp, so the structural range is
`0–255`. The official `psvr2_toolkit_capi_test` presents an ImGui `SliderInt`
from `0` to `25`. Nothing in the reviewed public path demonstrates that values
above 25 are valid or safe. This client enforces `0–25`.

### Zero and persistence

The official test allows zero. The driver forwards the byte without special
handling. No timer, duration, envelope or auto-off behavior exists in the C API
or the visible `HeadsetRumbleSet` path. Therefore:

- public source is consistent with `0 = stop`, but the firmware interpreting
  command `0x08` is not present in the repository;
- no automatic stop behavior is visible;
- this app ends every pulse with zero;
- the first hardware validation must confirm that zero actually stops the
  motor.

### Intensity and physical frequency

There is no separate intensity control. The parameter, structure and UI call
the value `rumbleHz`, but source code only forwards the byte. The public Toolkit
contains no physical calibration or measurement. Requested frequency must not
be described as intensity.

### Return value and failures

The send function returns `void`. If the driver is already inactive,
`submitCommand()` returns without queueing. If the driver disappears after that
check, the current wait loop for `isFulfilled` has no effective deadline despite
a comment referring to five seconds. The client therefore:

- calls native code away from the UI thread;
- serializes native calls;
- applies a timeout;
- blocks new native calls after a timeout;
- does not unload the DLL while a native call may still be running.

### Call rate

The driver thread calls `popCommand(10)` and comments that it runs roughly
every 10 ms. This is neither a 100-calls-per-second guarantee nor a documented
safe limit. The app uses its own policy of 20 non-zero calls per second.

### Multiple clients

The eight slots allow multiple clients for PCM/trigger effects.
`HeadsetRumbleCommand` carries no slot, and the rumble function does not check
`g_slot`. Any client can therefore overwrite global HMD rumble. There is no
cross-process priority.

### Headset presence and version

The C API exports neither headset presence nor an API version. An active driver
alone does not prove that an HMD is connected and accepting rumble. The app
shows headset presence as indeterminate and asks the user to perform a manual
test.

## Jailbreak and risk

The Toolkit README marks `Headset vibration*`; its footnote says certain
features require a jailbreak and can cause damage or brick the headset. This
app:

- does not perform a jailbreak;
- does not provide a jailbreak button or script;
- does not modify the Toolkit installation;
- displays a warning before use;
- directs the user to upstream documentation.

## Sources

- [C API header](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_toolkit_capi/psvr2tk_capi.h)
- [C API implementation](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_toolkit_capi/psvr2tk_capi.cpp)
- [Official loader](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_toolkit_capi_loader/psvr2tk_capi_loader.cpp)
- [C API test](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_toolkit_capi_test/main.cpp)
- [Shared manager](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/libcustomshare/custom_share_manager.cpp)
- [Driver command thread](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_openvr_driver_ex/command_thread.cpp)
- [Toolkit README](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/README.md)

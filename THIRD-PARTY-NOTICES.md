# Third-party notices

## Bibles included with Churchie

The files in `src/Churchie.App/Bibles/` are built by `tools/bibles/convert.py`. The Bible
text is unchanged; only the file format is converted.

| File | Translation | Licence | Source |
| --- | --- | --- | --- |
| `en-kjv.json.gz` | King James Version (1769) | Public domain | scrollmapper/bible_databases |
| `en-bsb.json.gz` | Berean Standard Bible | Public domain (dedicated 2023) | scrollmapper/bible_databases |
| `en-asv.json.gz` | American Standard Version (1901) | Public domain | scrollmapper/bible_databases |
| `en-bbe.json.gz` | Bible in Basic English (1949/1964) | Public domain | scrollmapper/bible_databases |
| `en-ylt.json.gz` | Young's Literal Translation (1898) | Public domain | scrollmapper/bible_databases |
| `en-web.json.gz` | World English Bible | Public domain ("World English Bible" is a trademark of eBible.org) | BibleNLP/ebible, from eBible.org |
| `yo-oycb.json.gz` | Open Yoruba Contemporary Bible (Bíbélì Mímọ́ Yorùbá Òde-Òní) | CC BY-SA 4.0 | BibleNLP/ebible, from eBible.org |

### Open Yoruba Contemporary Bible — required attribution

> Biblica® Open Yoruba Contemporary Bible™ Copyright © 2009, 2017 by Biblica, Inc.
> Original work available for free at www.biblica.com and open.bible.
> Licensed under the Creative Commons Attribution-ShareAlike 4.0 International License
> (http://creativecommons.org/licenses/by-sa/4.0/).

`yo-oycb.json.gz` is a reformatted copy of that work and is itself shared under CC BY-SA 4.0.
Churchie shows this notice in the Scripture tab whenever the Yoruba Bible is selected. This use
does not imply endorsement by Biblica, Inc.

### Berean Standard Bible

> The Berean Bible (www.berean.bible) Berean Standard Bible (BSB) is produced in cooperation with
> Bible Hub, Discovery Bible, OpenBible.com, and the Berean Bible Translation Committee.
> This text of God's Word has been dedicated to the public domain.

### scrollmapper/bible_databases (database format) — MIT licence

```
MIT License

Copyright (c) 2024 Scrollmapper

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### BibleNLP/ebible

Collected from eBible.org; each text keeps its original licence
(https://github.com/BibleNLP/ebible/tree/main/metadata/licenses).

## Libraries

- Avalonia UI — MIT licence — https://github.com/AvaloniaUI/Avalonia
- PortAudio (sound recording) — MIT-style licence — https://www.portaudio.com
- PortAudioSharp2 (.NET wrapper for PortAudio) — Apache 2.0 licence — https://github.com/csukuangfj/PortAudioSharp2
- CommunityToolkit.Mvvm — MIT licence — https://github.com/CommunityToolkit/dotnet
- SkiaSharp (unpacks animated GIF frames; comes with Avalonia) — MIT licence — https://github.com/mono/SkiaSharp
- LibVLCSharp (.NET wrapper for VLC) — LGPL 2.1 or later — https://code.videolan.org/videolan/LibVLCSharp
- VLC / libvlc (video playback) — LGPL 2.1 or later — https://www.videolan.org/vlc/libvlc.html

### VLC

Churchie plays videos with libvlc, VideoLAN's video engine, used unchanged as separate
library files that can be replaced:

- Windows: the `libvlc` folder next to `Churchie.exe`, from the official VideoLAN.LibVLC.Windows
  package (the LGPL edition, without VLC's GPL plugins).
- macOS: `Churchie.app/Contents/MacOS/libvlc`, copied from the official VLC 3.0.24 app by
  `build/get-vlc-mac.sh`, leaving out the same GPL plugins.
- Linux: the VLC installed on the computer (`sudo apt install vlc`); nothing is bundled.

The source code of VLC is available from https://www.videolan.org/vlc/download-sources.html
and https://code.videolan.org/videolan/vlc. VLC is a trademark of VideoLAN; Churchie is not
made or endorsed by VideoLAN. The LGPL 2.1 licence text: https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html
- xUnit (tests only) — Apache 2.0 — https://github.com/xunit/xunit

## Sample hymns

"Amazing Grace" (John Newton, 1779), "Blessed Assurance" (Fanny Crosby, 1873) and
"Holy, Holy, Holy" (Reginald Heber, 1826) are in the public domain.

# Pikvew — состояние и требования

Обновлено: 2026-11-04. Репозиторий: O:\STUDIO3\Repos\paulnsk\pikvew.

## Текущее состояние

- **Пакет 0.2.1 завершён и опубликован.**
  https://github.com/paulnsk/pikvew/releases/tag/v0.2.1
  EXE: https://github.com/paulnsk/pikvew/releases/download/v0.2.1/Pikvew.exe
  Проверено GitHub API: draft=false, asset uploaded, 79681215 bytes,
  SHA25661a1d92f0a149ed16c71513b7bf8e388774f85068ec11876fa162ae8e3d735df совпал.
  Tag commit21886f633921f936f9b39ae9271b8d122ccd7bb4; title/body ровно Pikvew 0.2.1.
  Все одобренные пункты реализованы. Осталась пользовательская проверка поведения
  на реальных UNC/мониторах; дополнительных блокирующих вопросов нет.
  C:\tools не обновляли (release -NoToolsCopy); publish.bat сохраняет эту возможность.

- Release candidate 0.2.1 собран: artifacts/release/win-x64/Pikvew.exe, 79681215 bytes.
  Bundle audit: 261 entries, 259 compressed, unwanted=[] (нет PDB/XML/source/log/TRX).
  Первый release.ps1 остановился на NU1900 (NuGet vulnerability endpoint недоступен),
  до build/upload. Повторно release.ps1 НЕ запускали, minor остался2; resume publish.ps1
  с WarningsNotAsErrors=NU1900 только для этого процесса, остальные warnings-as-errors
  сохранены. Зависимости без изменений, использован локальный package cache; свежая
  проверка уязвимостей недоступна. BuildFloor1, release commit21886f6 pushed/published.

- Финальный прогон пакета 0.2.x: 71 passed /4 Windows integration skipped, 75 total,
  artifacts/test-results/release-02.trx. Включает actual private JPEG decode/thumbnail,
  Ctrl-tree без H/V flicker, F2 file+current-directory, selection/history после rename,
  default delete action слева /Cancel справа. Скриншоты НЕ создавались.
  UI fixture после Restore() повторно уводит окно за экран, чтобы реальный курсор
  не менял hover routing; async navigation ожидается до конца, без100ms предположения.
  F2 ищет уже материализованный visual container и не перераскрывает текущее дерево.
  GitHub preflight успешен; релиз minor2/build1 опубликован (см. выше).

- Пакет 0.2.x реализован, идёт финальная проверка перед релизом. Rider solution Pikvew
  подтверждён. Его PSI отдельных файлов устарел: ImageLoader diagnostics ссылается на
  уже удалённый _thumbnailSlots и старые строки; новые partial files ещё не индексирует.
  Не применять эти quick fixes; компилятор актуального исходника проходит.
- Реализованы inline rename (F2/slow click), hover routing arrows/text/F2, visible-tree
  prefix search, Ctrl-tree local selection без прокрутки предков, first supported image,
  delete confirm action слева/default, Cancel справа/Esc, suppression double Open/OpenWith.
- JPEG SOI с посторонним текстом после FF D8: принудительный JPEG codec только по SOI,
  не по расширению. Actual private JPEG успешно декодирован + thumbnail без отображения.
- Thumbnail pipeline: read/decode раздельно; read limit адаптируется per drive/share,
  decode max half CPU, encoded queue256MiB, decoded budget256MiB..1GiB (oversized alone),
  jobs max2*halfCPU. Hidden browser отменяет thumbnail jobs. Benchmark generated48JPEG
  1920x1080, warm cache: old2slots2.908s/cpu5.812s; new0.673s/cpu6.047s,
  PeakDecoders10/CPUs20. Benchmark artifacts/thumbnail-bench (ignored). Slow network
  controller covered by deterministic test, real UNC not available here.
- Первоначально71 passed/4 OS integration skipped. Расширен UI regression Ctrl-tree /
  file+current-folder inline rename; UI test теперь await навигации вместо100ms race.
  Весь UI smoke по умолчанию НЕ делает screenshots; opt-in PIKVEW_UI_SCREENSHOTS=1.

- Rider MCP восстановлен: list_solutions подтвердил Pikvew.slnx, путь O:\STUDIO3\Repos\paulnsk\pikvew,
  22 tools. Начата реализация одобренного пакета 0.2.x. Скриншоты частных файлов запрещены.
  Источник Ctrl-navigation flicker: NavigateTreeAsync вызывает полную RevealTreeAsync,
  которая прокручивает каждого предка; для локальных moves сделать только selection /
  минимальный BringIntoView конечного узла, не полный reveal.
  Enter всегда preview; hover routing только arrows/text/F2, textbox edit имеет приоритет.
  Rename labels в tree и tiles, F2 browser only, remap paths/settings/history при rename.
  Thumbnails: раздельные read/decode очереди, per-volume adaptive read concurrency,
  decode до половины CPU, bounded encoded-memory queue; измерить read/decode отдельно.

- Ответы на вопросы получены: следующий пакет ОДОБРЕН, включая GitHub release
  0.2.x. Rename inline, file stem selected without extension, folder name whole,
  slow clicks только по подписи, fast double-click opens, current item only;
  F2 только browser (НЕ viewer). Ctrl+arrows всегда tree; обычные arrows/text/F2
  target hover panel, иначе real focus. Enter открывает current preview card даже
  при mouse-over tree. После tree navigation — первая supported image, иначе первый
  обычный файл. Prefix search starts-with/case-insensitive, 1s timeout, Backspace,
  только видимые раскрытые узлы. Подтверждено «делаем релиз».
- Повторная проверка после просьбы «Проверь райдер щас»: ALL_TOOLS по name/description
  rider|jetbrains|resharper по-прежнему пуст. Нельзя вызвать list_solutions, соединение
  с Pikvew не подтверждено. До восстановления Rider либо явного разрешения обходиться
  без него изменения исходного кода НЕ начаты; пока изменены только заметки/правила.

- 2026-11-04: пользователь запросил следующий пакет и сначала вопросы; код пока
  НЕ менять. F2/два медленных клика rename файлов/папок; Ctrl+arrows tree flicker /
  node scroll to bottom; mouse-hover panel determines keyboard target (fallback real
  focus); incremental prefix search tree+tiles (fo->forest, fot->fotki), subtle hover
  border. После Ctrl+tree navigation выбрать первый файл для Enter. Delete confirm
  action слева/default, Cancel справа (переопределяет прежний Cancel-default).
  Double-click Open не должен запускать дважды, проверить OpenWith. Adaptive thumbnail
  I/O/decode concurrency до половины CPU для local, не перегружать медленный network.
  K:\DELETEME\154713090178_x1x.jpg не декодируется. Файл доступен, header 869343 bytes:
  JPEG SOI FF D8, затем ASCII " end='w'?>", затем FF DB. Возможная причина — strict
  format autodetection на нетипичном заголовке; декодирование ещё НЕ проверялось.
- ПРИВАТНОСТЬ: не делать screenshots с изображениями пользователя. Для UI screenshots
  только публичные образцы, анализ частного JPEG только без визуализации. В AGENTS
  правило добавлено. Pending уточнения rename UX/search scope/Enter routing/first
  supported image vs any file/release scope. Rider tools в текущем наборе не обнаружены;
  перед реализацией восстановить Rider MCP или получить разрешение обходиться без него.

- **Седьмой проход завершён, GitHub release 0.1.1 опубликован и проверен.**
  https://github.com/paulnsk/pikvew/releases/tag/v0.1.1
  EXE: https://github.com/paulnsk/pikvew/releases/download/v0.1.1/Pikvew.exe
  GitHub draft=false, asset state=uploaded, 79666509 bytes; remote SHA256 совпал
  с локальным 4687dc096b5786f7fc3f338145dcf4d823f5b5c0688efb2a331729bb22c2b5a6.
  Tag commit d3a70c53b00132d229e0c26c0a768d0200977c0c; title/body ровно Pikvew 0.1.1.
  Все одобренные функции реализованы, 60 tests passed /4 OS integration skipped;
  проверить реальные мониторы/DPI/UNC и реальные удаления пользователю вне sandbox.
  В этом выпуске C:\tools не обновляли: release script собирает/публикует artifact;
  локальное копирование C:\tools сохранено в publish.bat / scripts/publish.ps1.

- Release candidate **0.1.1** опубликован локально: artifacts/release/win-x64/Pikvew.exe
  79666509 bytes, SHA256 4687dc096b5786f7fc3f338145dcf4d823f5b5c0688efb2a331729bb22c2b5a6.
  Final bundle audit: 261 entries, 259 compressed, unwanted=[]; artifacts/release-bundle.json.
  Feature commit774b7e6, version release commit3aec2d9, transport fix d3a70c5 pushed main.
  Git HTTPS требует Basic token, REST Bearer; helper поправлен, credentials не раскрыты.
- GitHub release ID **430439481**, tag v0.1.1, title/body Pikvew 0.1.1, target
  commit d3a70c5. Upload+digest verification+publish завершились успешно.
  Повторный release.ps1 намеренно создаст следующий minor, 0.2.x.

- Финальные проверки седьмого прохода: **60 passed, 4 skipped, 0 failed / 64**.
  Skips — 3 registry cases + pipe integration, включаются в test.bat вне sandbox.
  UI smoke проверил enter папки, Up select departed folder, Back/Forward, native
  folder icon >=128px, I/metadata rows/Pin, оба custom delete confirms (Cancel default,
  icon46/64). Счётчик версий reset/increment проверен отдельным тестом. Rider warnings0.
  Просмотрено 4 скриншота: browser folders / metadata / permanent dialog (старый crop
  + итоговый полный); новый диалог borderless с rounded frame и theme resources.
- Дополнительно исправлены новые внешние папки после empty probe: LoadAsync(refresh)
  с merge существующих DirectoryNode вместо Clear, lookup miss при reveal перечитывает
  родителя. Тест сохраняет identity/selection существующих узлов.
- На Temp с >2700 папок выявлен recycle ancestor во время programmatic reveal.
  Tree использует Standard virtualization + pixel scrolling; короткая цепочка
  контейнеров удерживается до окончания queued scroll (ref-count для concurrent
  reveals), index -1 не передаётся в VSP, реальные async FS enumeration не блокируются.
  Программные container events не портят историю. Диагностический listener удалён.
- Готовится commit и первый release через scripts/release.ps1; все остальные
  исходные изменения завершены. Финальный EXE ещё НЕ загружен; следующий шаг release.

- Седьмой проход реализован: folder metadata/cards + native 256px folder icon,
  folder-only navigation, history snapshots и квадратный Up / круглые Back/Forward,
  Alt shortcuts/tooltips, selection при Open/OpenWith, idle cursor и edge24 overlays,
  common EXIF/IPTC/XMP reader + table/I/Pin/copy, themed delete confirm Cancel default,
  mixed recycle/permanent batches подтверждаются ДО первого удаления. Read metadata
  закрывает file handle; JPEG EXIF проверен. MetadataPinned в registry.
- Выявлен тестом и исправлен history Forward: container realization при RevealTree
  мог породить ложную user selection; локальный guard только на синхронную реализацию
  контейнеров, фоновые перечисления UI не блокируют.
- Версионирование: Version.props (Major 0 / Minor 0 до первого release), build target
  только для App, atomic local counter artifacts/version-counter.txt, reset tests
  0.1.1 -> 0.1.2 -> 0.2.1 -> 1.1.1 -> 1.1.2 прошли. MinorVersionMajor marker позволяет
  вручную менять только Major. Release script поднимет minor до 1, BuildFloor сохраняет
  опубликованный build для нового checkout. README сокращён до описания + shortcuts.
- Публикации для сравнения: artifacts/bench/plain/Pikvew.exe 173827212 bytes,
  compressed 79664861 bytes. Native bundle manifest parsed scripts/audit-bundle.py:
  261 entry, compressed 259; PDB/XML docs/source/log/TRX нет. Satellite languages=en.
  Сжатие оставляем; AOT/trimming выключены для WPF.
- Startup measurements scripts/measure-startup.ps1 (hidden own processes, до создания
  main window, НЕ до decode): plain fresh-extraction1511.3ms, warm588.5/605.7/598.1;
  compressed fresh-extraction2652.5ms, warm649.2/614.7/608.3. OS filesystem cache warm
  во всех случаях; результаты artifacts/bench/startup.json, это не cold-disk benchmark.
- GitHub preflight успешен: paulnsk/pikvew push=true, releases=0. GCM credentials
  доступны через прямой вызов git-credential-manager.exe с username=paulnsk.
  PowerShell HTTPS в sandbox не работает; scripts/github-api.py использует Python
  stdlib TLS + Git CA bundle, никаких токенов в файлах/логах. Release.bat требует
  .NET10 SDK + Git for Windows/GCM + Python. Релиз ещё НЕ создан, нужен final test /
  commit и scripts/release.ps1 (сам publish, bump minor, commit/push, draft+upload+
  digest verification+publish; title/body только Pikvew version).

- Седьмой проход ОДОБРЕН: fullscreen idle cursor 1s только над изображением;
  папки-карточки (только навигация, всегда раньше файлов и видны при SupportedOnly);
  Up первым квадратным, Back/Forward круглыми, history restores selection/scroll,
  Up selects departed folder, Alt+Up/Left/Right; hotkeys в tooltips. Open/OpenWith
  сначала выбирают tile. Справа metadata overlay: edge 24 DIP, bottom тоже 24,
  hide 2s после ухода, I/pin, window+fullscreen, common EXIF/IPTC/XMP + file info.
  Delete всегда theme-aware custom confirm с Cancel default; permanent красный
  крупный question glyph, UNC/no recycle тоже permanent confirmation.
- Версии major.minor.build: Major только пользователь, сейчас 0. Minor +1 при
  GitHub release; Build +1 на реальный build приложения, reset=1 при major/minor;
  minor reset=1 при major. Реализовать правила, короткий README+неочевидные hotkeys,
  первый GitHub release 0.1.x с published self-contained single EXE; title/body
  только Pikvew + version. Release/commit/push авторизованы без новой отмашки.
- AOT/trimming не включать для WPF. Проверить bundle contents на PDB/мусор и
  сравнить single-file compression по размеру/запуску; не называть замеры cold disk
  (fresh compiled files в OS cache). После проверки выбрать compression.

- Шестой проход: AssociationDialog теперь выбирает только цель операции, все
  галочки изначально сняты. Две кнопки Add registration / Remove registration
  (disabled без выбора); Add объединяет отмеченные с текущей регистрацией, Remove
  исключает только отмеченные. Остальные расширения сохраняются. Длинный текст убран.
  Предыдущее поведение «выбор = полный набор регистраций» отменено. Windows default /
  UserChoice по-прежнему не трогаем; переход в Windows Settings сохранён.
  RegistryFact обновлён: последовательное добавление PNG/WebP, затем GIF+PNG другой
  регистровости; удаление только PNG + отсутствующего JPG сохраняет GIF/WebP/default.
  Rider inspections: 0 warnings/errors в четырёх изменённых C# файлах. Build/test /
  publish в этом проходе не запускались (экономия лимита); опубликованный EXE прежний.

- Пятый проход: горизонтальные рывки дерева при выборе/раскрытии. Убран явный
  ScrollToHorizontalOffset из BringTreeHeaderIntoView; DirectoryTreePanel реализует
  IScrollInfo.MakeVisible с видимым горизонтальным срезом, оставляя WPF вертикальную
  прокрутку/виртуализацию. Горизонтальная прокрутка остаётся ручной.
- В AssociationDialog уточнено: снятая галочка убирает capability/OpenWithProgid /
  SupportedTypes, но НЕ сбрасывает уже выбранную пользователем default association.
  Иначе можно было бы оставить UserChoice, указывающий на удалённый ProgID; сам ProgID
  сохраняется. Для смены default выбрать другую программу в Windows Settings.
  Пустой зарегистрированный набор теперь отображается пустым, без автоматического
  включения всех галочек. Для первичной массовой регистрации есть Select all.
- Пользователь сообщил, что queued-вопросы не видны в его консоли. Больше не вызывать
  request_user_input_async; при необходимости остановиться и спросить обычным текстом.
  Правило добавлено в AGENTS.md. UNC MP4 был на другом компьютере, пример недоступен;
  задержку VLC не воспроизвели, не чиним предположениями, дополнительные пути не просим.
- Проверка пятого прохода: Debug build успешен; два целевых WPF regression tests
  FolderTreeKeepsHorizontalPositionWhileExpandingAndRevealing прошли для offset 0/80.
  Проверены expand невыбранной ветки, сохранение selection, vertical reveal дальнего
  узла и отсутствие промежуточных horizontal ScrollChanged. Rider inspections чистые.
  Полный suite и publish не запускались, опубликованный EXE прежний.

- Четвёртый проход: пользователь разрешил ассоциации и попросил Ctrl+arrows для
  дерева без перевода фокуса, Open default + Open with на unsupported и app icon.
  Реализованы кнопка File associations с checkbox-диалогом, HKCU Capabilities /
  RegisteredApplications / ProgID / OpenWithProgids. Default/UserChoice не меняем:
  Windows 10/11 требует системное подтверждение; после регистрации открываем
  Settings (Windows 11 deep link registeredAppUser=Pikvew, Windows 10 общая страница).
  Регистрация по текущему расположению exe, после переноса нужно повторить.
- Unsupported: две кнопки в браузере и viewer, Open запускает ShellExecute open;
  Enter в браузере и unsupported viewer делает Open, browser double-click тоже.
  Default app icon через AssocQueryString(EXECUTABLE)+ExtractIconEx, fallback
  extension-only SHGetFileInfo(USEFILEATTRIBUTES), фоновая очередь 2, cache 128
  extensions/1 minute. Иконка не обращается к реальному UNC-файлу.
- Ctrl+arrows в browser обходят видимые узлы/expand/collapse/parent/first child;
  фокус сохраняется; текстовые поля/ComboBox сохраняют свои клавиши. Модельный
  DirectoryNavigation покрыт тестом, регистрация покрыта изолированным RegistryFact.
- Память: ThumbnailCache LRU 64 MiB; ThumbnailImage освобождает Source при Unloaded.
  Это НЕ общий лимит процесса. ImagePreloader ограничен 4 документами (обычно 2
  соседа), без бюджета decoded bytes; 128 MiB — только ограничение encoded размера
  для speculative preload. GIF snapshot cache 512 MiB НА ДОКУМЕНТ, плюс decoder /
  encoded data / first frame. Общую память не измеряли; Pikvew при осмотре не запущен.
- Задержка Open with -> VLC на UNC: код сразу вызывает SHOpenWithDialog, запуск
  выбранной программы полностью внутри Windows. Причина НЕ установлена, поведение
  вызова/флаги не меняли. Добавлен bounded timing log %LOCALAPPDATA%\Pikvew\shell.log
  (256 KiB + один .old): вход/возврат native API, WM_ENABLE owner, Default open.
  Время SHOpenWithDialog включает время выбора пользователя. Запрошены полный
  UNC-путь MP4 и сравнение с Explorer; до ответа не запускать случайные видео.
- Этот проход: только Rider inspections, без build/test/publish по просьбе экономить
  лимит. Новые тесты добавлены для пользовательского запуска. EXE пока прежний.

- Третий проход (экономия лимита, пользователь разрешил не запускать проверки):
  1) compressed image mouse-down -> 100% у курсора + немедленная возможность pan;
  expanded image click-release без drag -> fit, порог движения системный.
  2) View-only 90° rotation, Ctrl+Left/Right и две кнопки тулбара; поворот сбрасывается
  на новом изображении, работает с анимацией/preview и корректирует размеры viewport.
  3) Loaded/selected узлы проверяют наличие хотя бы одной подпапки в фоне,
  отдельный лимит 2 probes, у пустых убирается expander. Недоступные не считаем пустыми.
  4) Диалог ассоциаций расширений отложен с разрешения пользователя ради лимита.
- Изменения третьего прохода НЕ собраны, НЕ протестированы и НЕ опубликованы:
  пользователь сам запустит проверки в Studio. EXE в artifacts пока предыдущий.

- 2026-11-01, второй проход по пользовательской приёмке: исправить пункты 1–17
  и Ctrl+wheel/максимум превью 640. Главные: гонка tree reveal/выбор и кража фокуса;
  скролл selected tree header; тёмные TreeViewItem/ScrollBar/Slider/MenuItem;
  единая верхняя панель без Up/Browse, необязательный breadcrumb; сохранение Fit
  между файлами; fullscreen toolbar только у нижней кромки; контекст для unsupported;
  карточки unsupported; собственный SVG->ICO и пиктограммы плеера/fullscreen.
- Производительность проверять на реальных файлах K:\ScanImage001.png
  (14032×10208 RGB8, 342MB) и K:\4685896-bdsmlr-10356-r8vcfiol2b.gif (19MB).
  Предзагрузка нужна в обе стороны. test.bat починить с постоянным логом и pause.
  Предыдущих пользовательских test-логов нет: старый bat их не сохранял.
- Второй проход реализован: устранены ShowBrowser/reveal гонка и перевод фокуса
  с дерева; реализация/прокрутка selected header через DirectoryTreePanel (проверка
  500 папок), собственные TreeViewItem/ScrollBar/Slider/MenuItem templates.
- Сверху filter/sort/thumbnail slider 64–640 + clickable breadcrumbs; Up/Browse/
  редактирование пути и нижняя полоса убраны. Ctrl+wheel меняет размер превьюх.
- Fit (*) теперь sticky для следующих файлов и сохраняется в registry;
  / возвращает исходную политику маленьких картинок 100%. Ручной зум не переносится.
- Предзагрузка в обе стороны и сохранение покинутого ImageDocument. Версия
  документа хранится отдельно от обновившихся metadata (иначе кеш подменял новый
  файл старым после внешней замены; соответствующий тест поймал и проверил фикс).
- GIF: при пропуске таймером кадра декодируем промежуточные последовательно,
  а не заново весь префикс; кеш decoded frames до 512 MiB на документ.
  Реальный пользовательский GIF: load 0.089s, все 119 кадров 1.876s, повторный
  кешированный проход <1ms; длительность записи 4.957s. Это замер декодера, не FPS UI.
- PNG: обычные неанимированные PNG через WIC, без Skia/AnimatedImage probes;
  оригинал сохраняется, для fit >16MP строится отдельный preview до 4096px.
  На K:\ScanImage001.png end-to-end loader+preview 2.337s (локальный benchmark),
  WIC decode 2.136s; исходный файл не изменялся. При зуме используется оригинал.
- Fullscreen toolbar только у нижней кромки / при наведении на саму панель;
  unsupported viewer имеет общее context menu, Copy image disabled при отсутствии
  пикселей; unsupported карточки заполнены большой Open With кнопкой без дубля текста.
- Нарисован собственный Assets/Pikvew.svg, сконвертирован в ICO 16–256px и встроен;
  play/pause/frame/fullscreen — векторные пиктограммы. imagegen не использовался.
- test.bat теперь cmd /k launcher + scripts/run-tests.ps1 (явный dotnet.exe),
  сохраняет UTF-8 test.log и tests.trx в artifacts/test-results, не исчезает после
  завершения. Sandbox-параметр runner отключает только OS integration tests.
- Второй проход, текущая проверка: **52 passed, 3 skipped, 0 failed** (55 cases),
  через новый runner; UTF-8 test.log и tests.trx действительно записываются.
  Дополнены проверки: 500 директорий, выбор/прокрутка/сохранение фокуса дерева,
  sticky Fit, unsupported context menu, original+preview большого PNG,
  возврат к тому же ImageDocument при навигации назад/вперёд.
- Скриншоты просмотрены в пределах согласованных 5: dark scrollbar/slider,
  uniform menu без icon gutter, vector playback/fullscreen glyphs. Fullscreen
  toolbar по умолчанию скрыт; показ только у нижней кромки/на самой панели.
- Виртуализация дерева сохранена: целевой container реализуется по цепочке
  DirectoryTreePanel, затем явно выбирается и прокручивается только header.
  Горизонтальная прокрутка не должна скрывать начало имени выбранной папки.
- Код второго прохода готов, финальная single-file публикация завершена:
  artifacts/release/win-x64/Pikvew.exe, 182751077 bytes; выполняется commit/push.
- Дополнительный smoke запуска опубликованного EXE не прошёл стадию secondary
  instance forwarding: single-instance mutex уже существует (bench --instance-info
  подтвердил IsPrimary=False), подключение к pipe запрещено sandbox. Окно ошибки
  созданного нами secondary экземпляра закрыто. Не считать этот запуск проверкой
  свежего процесса; пользователю закрыть предыдущую копию перед проверкой новой.

- Репозиторий при первом осмотре содержит только .git.
- Этап 1 выполнен: Pikvew.slnx, WPF shell, DI/IHost, Core и xUnit-проект,
  manifest PerMonitorV2/long paths/asInvoker, publish.bat и конфигурация репозитория.
- Этапы 2/3 реализуются вместе. Restore/build/test/publish уже выполнялись;
  первоначальное ограничение этапа 1 больше не действует.
- Продуктовые вопросы закрыты ответами пользователя; уточнения внесены ниже.
- Rider MCP подключён, решение Pikvew подтверждено. Пользователь разрешил обычные
  инструменты для редактирования и build/test; Rider для навигации/инспекций.
- В работе этапы 2/3: разрешено реализовывать полноценный UI сразу, без отдельной
  отмашки на переход между этапами. Существенное расширение scope согласовывать.
- Пользователь хочет закончить за вечер, без растягивания на недели.

### Реализовано в текущей работе

- Декодирование Magick.NET-Q8-x64 15.0.0, GIF/WebP через SkiaSharp 4.153.1;
  AnimatedImage 2.1.4 только как managed APNG fallback. Отдельный AnimatedImage.Native
  удалён: для GIF/WebP используется native Skia с одним декодированным canvas,
  без накопления распакованных GIF-кадров. Наш код отвечает за часы/команды UI,
  библиотеки — за декодирование, зависимости кадров и disposal.
- ImageLoader закрывает исходный файл до декодирования; EXIF AutoOrient,
  преобразование ICC в sRGB; SVG принудительно через RSVG без Inkscape.
  Для ICO/TGA явно выбирается формат. Многостраничный TIFF: первая страница + notice.
- WPF браузер: дерево с ленивой фоновой загрузкой (максимум две одновременно),
  виртуализированная сетка, размер превьюх, фильтр, сортировки, выделение,
  адресная строка/UNC, drag-and-drop, кнопка Open with для неподдерживаемых файлов.
- Отдельные очереди превьюх и основного просмотра; LRU превьюх 64 MiB;
  предзагрузка одного следующего статичного файла; отмена устаревших запросов.
- Просмотр: физические пиксели, начальный fit без увеличения маленьких,
  zoom/pan/клавиши, перемотка/пауза/шаг по кадрам, fullscreen overlay.
- SingleInstance: session mutex + current-user-only named pipe; обработка
  внешнего открытия в существующем окне, сохранение происхождения для Esc.
- WindowPlacement: штатные native workspace coordinates, PerMonitorV2,
  fullscreen на текущем мониторе, обработка WM_DPICHANGED/WM_DISPLAYCHANGE.
- SettingsCodec + RegistrySettingsStore (HKCU), включая множественное выделение.
  Собственные читаемые стили и переключение системной светлой/тёмной темы;
  поля изображения всегда чёрные. Fluent ThemeMode заменён после проверки контраста.
- Контекстные меню: копирование bitmap+PNG текущего кадра, файлов и путей;
  copy/move selection с раздельными destination, collision rename/skip;
  Delete/Control+Delete по согласованной политике; штатный Open With.
- Внешние изменения: асинхронный polling раз в 3 секунды с сравнением metadata;
  неизменившийся список не пересоздаётся. FileSystemWatcher пока не используется.
- publish.bat: single-file self-contained, зелёный/красный фон, copy to C:\tools
  если существует. Путь публикации теперь artifacts/release/win-x64.
  Native PDB исключены из публикации. test.bat включает проверки registry/pipe.

### Проверки и текущие ограничения среды

- Последний завершённый прогон: **50 passed, 3 skipped, 0 failed** (53 cases).
  Сборка тестов без compiler warnings/errors. Пропуски: два registry и один pipe;
  все три включаются через test.bat вне sandbox.
- Финальные Rider daemon inspections без warnings/errors для MainWindow,
  MainViewModel, ImageLoader, ImagePreloader, AnimationDecoder, DirectoryNode,
  WindowPlacement, SettingsCodec, SingleInstance, ThumbnailPanel и App.
- Последние доработки: дерево раскрывается до текущей директории без ожидания UI;
  устаревшие внешние запросы не перебивают новые; обновление/удаление файла
  сохраняет позицию навигации и обрабатывает пустую папку (покрыто тестами).
  Даже первоначальное открытие UNC-файла вынесено с UI-потока;
  file sharing допускает read/write/delete во время чтения. TIFF notice теперь
  в панели статуса, а не поверх изображения.
- Последний полностью завершённый прогон до последних небольших изменений:
  47 passed, 2 skipped (registry). После добавления pipe integration выявлен
  AccessDenied при подключении к named pipe в sandbox; этот тест тоже вынесен
  в явный запуск вне sandbox через test.bat. Это НЕ считается проверенной
  работой IPC. Требуется пользовательская проверка/прогон test.bat.
- Реальная запись registry также AccessDenied в sandbox; codec проверен отдельно.
- Скачаны GIF/WebP/APNG и PNG-эталоны из whistyun/AnimatedImage,
  revision fb3714b6cfdbdae734e33b523e80c303bdfdae52. Локально artifacts/samples,
  в git бинарные образцы не включаются. Повторное получение scripts/fetch-samples.ps1.
- Автоматические проверки: core сортировка/границы/Esc/геометрия/timeline,
  static JPEG/PNG/BMP/WebP/TIFF/ICO/TGA/AVIF/JXL, GIF/WebP frames+seek,
  EXIF, RSVG, two-minute GIF, интернет-эталоны GIF/WebP/APNG, browser cancellation,
  metadata refresh/filter/sort, settings codec, UI smoke с реальным WPF окном
  (виртуализация, player, browser/viewer/fullscreen, навигация).
- Проверочная self-contained single-file публикация выполнена. В новой папке
  artifacts/release/win-x64 только Pikvew.exe (~182 MB); debug symbols и
  посторонние runtime файлы не публикуются. Старый artifacts/publish/win-x64
  содержит остатки первого эксперимента: очистка была отклонена автопроверкой,
  поэтому новая папка используется без удаления старой.
- EXE запускался из artifacts/release/win-x64 с реальным stickman.webp.
  Через UIAutomation подтверждены image dimensions, Pause и seek slider;
  вызваны пауза, перемотка, штатное закрытие; процесс завершился успешно.
  Финальный EXE перепубликован после последних исходных правок; повторно проверены
  WebP с рабочим slider/pause и статический PNG (100 × 100) через UIAutomation.
  Оба запуска завершены штатным закрытием с успешным выходом процесса.
  В выходной папке один Pikvew.exe, 181 993 317 bytes.
- Реальные разные мониторы/DPI и UNC с пользовательскими ресурсами, Open With,
  clipboard insertion, корзина/переносы требуют ручной проверки пользователя.
- Внутри sandbox NuGet через Windows SSL/proxy не скачивает отсутствующие runtime
  пакеты. Для проверочной публикации официальные nupkg 10.0.14 скачаны Python urllib
  в artifacts/publish-feed с обычной проверкой TLS; использован локальный feed,
  RestorePackagesPath=artifacts/publish-packages и fallback read-only user cache.
  Пользовательский publish.bat остаётся обычным dotnet publish.

### Следующие действия

- Реализация готова к пользовательской приёмке. Финальные build/publish и
  smoke EXE завершены. Остались проверки настоящих мониторов/UNC/clipboard/
  Open With/корзины и запуск test.bat вне sandbox (registry + IPC).
- README содержит запуск, клавиши, публикацию, архитектуру и проверки;
  publish.bat/test.bat сохранены с CRLF, UTF-8 без BOM.
- Коммит/push разрешены. После проверок можно фиксировать реализацию.
- Передать EXE пользователю для проверки настоящих мониторов/UNC/Windows-интеграций.

## Этапы и критерии перехода

### 1. Скелет

- Стек: WPF + .NET 10, Windows 10/11 x64.
- Создать решение .NET 10, минимальное приложение, DI/IHost, структуру модулей,
  тестовые проекты и конфигурацию репозитория. Не реализовывать будущие слои заранее.
- На этом этапе ничего не проверять: не запускать сборку, тесты или приложение.
- Создать publish.bat: dotnet publish, win-x64, self-contained, single-file.
  Учесть упаковку native-библиотек: при необходимости извлечение во время запуска;
  single-file не означает отсутствие runtime extraction.
- Передать пользователю путь к решению. Пользователь открывает его в Rider
  и включает Rider MCP. Уточнение пользователя: MCP для навигации/инспекций,
  редактирование и проверки обычными инструментами разрешены.

### 2. Проверка основы

- Подтвердить доступ к нужному решению через Rider MCP.
- Проверить декодеры на реальных скачанных образцах; записывать источник,
  ожидаемое поведение, результаты и ограничения. Предложен Magick.NET,
  конкретный пакет/набор декодеров пока не утверждён и не проверен.
- Поддерживать все обычные форматы, которые даёт выбранная библиотека.
  Обязательно отдельно проверить анимированные GIF и WebP; также проверить APNG.
  HEIC/HEIF/JXL/AVIF не требуют отдельной реализации: если доступны из коробки,
  включить. SVG — желательный бонус. HEIC/JXL не являются подформатами JPEG.
- Сделать минимальный диагностический UI, достаточный для проверки декодирования,
  анимации, зума, навигации, single-instance и окон/DPI, без полноценной отделки.
- Проверить временные задержки, прозрачность, сборку разностных кадров, циклы,
  паузу/перемотку/покадровый просмотр и ограничение памяти длинных анимаций.
- По возможности использовать готовый проигрыватель: сначала изучить варианты.
  Пользователь предпочитает библиотеку самодельному плееру. Собственный механизм
  при необходимости обязан поддерживать перемотку и покадровый просмотр.
- Проверить фоновые операции, отмену устаревших запросов, сортировку,
  файловые операции, буфер обмена, сохранение настроек и обработку ошибок.
- Написать и запустить содержательные автоматические тесты. Позвать пользователя
  на визуальную проверку и проверки реальных мониторов/сетевых путей.
- До полноценного UI убедиться, что выбранная основа работает.

### 3. Полноценный UI

- Собрать браузер, просмотр, меню и панели по требованиям ниже.
- Проверить горячие клавиши, выделение, возврат в браузер, сетевые сценарии,
  внешние изменения, восстановление состояния и межмониторный fullscreen/DPI.
- Завершить тесты, пользовательскую приёмку и инструкции запуска/сборки.
- Коммиты/push разрешены; фиксировать фактически выполненные проверки.

## Согласованные требования

### Платформа и форматы

- Windows 10/11 x64, WPF + .NET 10, без Node.js/Electron.
- publish.bat публикует self-contained single-file exe со встроенным .NET.
- Скрипт красит всю консоль в зелёный при успехе и в красный при ошибке;
  если C:\tools существует, копирует туда Pikvew.exe с заменой. Ошибка копирования
  также считается ошибкой. Скрипт не запускает exe и ожидает нажатия клавиши.
- Настройки хранить в HKCU\Software\Pikvew, без переносимых JSON рядом с exe.
  Реализация registry store и её тесты относятся к этапу 2.
- Главный сценарий — картинки из интернета, особенно анимированные GIF и WebP.
- SVG полезен, но необязателен; RAW/PSD не приоритет.
- Многостраничный TIFF: полноценный просмотр только если библиотека даёт его
  практически из коробки; иначе первая страница с уведомлением о многостраничности.
- Неподдерживаемые файлы должны позволять Open With.
- Корректные EXIF-ориентация и цветовые профили/палитры.

### Окно, single-instance, мониторы

- Одно окно и один экземпляр программы.
- Запуск без параметров открывает браузер.
- Запуск с файлом открывает просмотр сразу, в сохранённом режиме window/fullscreen;
  браузер/состояние папки готовятся в фоне, навигация по соседям доступна максимально быстро.
- Повторный запуск с другим файлом передаёт его существующему экземпляру,
  открывает в том же окне, обновляет текущую директорию; сохраняет корректный режим окна.
- Enter/двойной клик переключают браузер и просмотр (при просмотре возвращают браузер).
- Esc при внешнем открытии картинки (Explorer/Total Commander, включая повторное
  открытие в существующем экземпляре) закрывает приложение; при входе в просмотр
  из браузера Pikvew возвращает браузер. Хранить происхождение текущего входа
  в просмотр, а не только первоначальный способ запуска процесса.
- Enter/двойной клик работают независимо от происхождения запуска. Новый вход
  в просмотр из браузера даёт Esc -> браузер.
- Esc никогда не переключает fullscreen в оконный просмотр.
- Средняя кнопка / Ctrl+Enter переключают окно и fullscreen.
- Fullscreen на том мониторе, где находится окно; корректный per-monitor DPI.
- Запоминать геометрию окна, монитор, режим, последнюю папку, выделение,
  прокрутку, размер превьюх, сортировку и остальные пользовательские настройки.
- Обработать отключение монитора/изменение конфигурации без потери доступности окна.

### Браузер и навигация

- Слева дерево директорий, справа превьюхи; слайдер их размера.
- Поддержка множественного выделения.
- Колесо в просмотре аналогично PgUp/PgDn: предыдущий/следующий файл.
- Home/End — первый/последний файл. На границах остановка, без зацикливания.
- Навигация соответствует текущей сортировке браузера.
- Естественная сортировка чисел и обычная по имени; настройки сохраняются,
  конкретный вариант по умолчанию несущественен.
- Сортировка по дате допустима; не добавлять прочие варианты без необходимости.
- Браузер может показывать все файлы, неподдерживаемые — с Open With.
  Добавить сохраняемый checkbox "Supported formats only", чтобы отсеивать
  посторонние файлы в больших папках. Не скрывать ошибочно явно открытый файл.
- Drag-and-drop файлов/папок, открытие из Explorer и Total Commander.
- UNC и сетевые пути, включая \\1.2.3.4\c$\.
- Дерево директорий не должно блокировать просмотр/навигацию, особенно в сети.
- Асинхронная загрузка, фоновые операции, виртуализация/кеш превьюх,
  предзагрузка соседей, разумные лимиты памяти; не запускать всё бесконтрольно.
- Отслеживать внешние изменения; предусмотреть ограничения watcher на сетевых ресурсах.
- Не держать исходный просматриваемый файл открытым.

### Масштабирование

- При открытии каждого нового файла: большие вписать, маленькие показывать 100%.
- Не переносить ручной масштаб предыдущей картинки на следующую.
- Ctrl+колесо и +/- — зум; / — 100%; * — fit, включая увеличение маленьких.
- Если картинка больше viewport, перетаскивать мышью.
- 100% означает один пиксель изображения на один физический пиксель экрана,
  независимо от Windows DPI. Умножение на DPI пока не делать.
- Ctrl+колесо масштабирует относительно курсора, +/- относительно центра viewport.
- Стрелки влево/вправо выполняют pan наряду с перетаскиванием мышью.

### Анимация и панели

- Автоматическое воспроизведение анимированных форматов.
- Обязательны пауза, перемотка слайдером (в том числе двухминутных GIF),
  понятный покадровый просмотр. Конкретный UX пользователь доверил разработчику:
  простые кнопки предыдущего/следующего кадра, play/pause, время и слайдер.
- Всегда зацикливать анимацию независимо от записанного loop count.
- В оконном просмотре панель анимации видна для анимированных файлов;
  fullscreen — всплывает при движении мыши и исчезает через пару секунд.
- Переключение фона прозрачности в оконном просмотре; если это просто.
- Компактный статусбар: размеры ценны; не показывать номер файла в списке.
- Интерфейс английский. По возможности определять системную тему, иначе тёмная.
  Свободное поле вокруг картинки всегда чёрное, а не серое, независимо от темы UI.

### Буфер обмена и файлы

- «Копировать картинку» — пиксели, поведение максимально как Chrome.
  Проверить реальные форматы clipboard для статичных/анимированных файлов;
  не обещать сохранение анимации при вставке в произвольное приложение.
- Добавить копирование исходного файла и пути.
- Копировать/переместить в директорию: для всего выделения, единичный файл — частный случай.
- Раздельно помнить последний destination для copy и move; подставлять в диалог.
- При коллизии предложить пропуск или переименование; заранее предложить первое
  свободное имя: pic.jpg + pic_1.jpg => pic_2.jpg. Без молчаливой перезаписи.
- Delete — корзина по умолчанию, Ctrl+Delete — постоянное удаление.
- Подтверждение удаления только если выбрано несколько файлов, одинаково для
  Del/Ctrl+Del и независимо от доступности корзины. Один файл удалять молча.
- Пользователь явно отклонил дополнительное предупреждение для сетевого удаления:
  если корзина недоступна, удаление постоянное, без отдельного предупреждения;
  для нескольких файлов остаётся одно общее подтверждение.
- F2/переименование и «Показать в Проводнике» не нужны.
- Open With: использовать штатный Windows диалог с ассоциированными приложениями
  и выбором другой программы. Собственное меню приложений не требуется.

## Открытые вопросы

- Блокирующих продуктовых вопросов нет. Существенные новые ограничения,
  необходимость смены библиотеки или расширение scope согласовывать по фактам.
- Проверить технически готовые animation-компоненты и native single-file publish
  на этапе 2, не выдавать пока это за проверенные возможности.

## Журнал решений и проверок

- 2026-10-31: зафиксированы требования пользователя; код/сборка/тесты не запускались.
- 2026-10-31: внесены окончательные уточнения: publish.bat/single-file, Esc по
  происхождению просмотра, физические пиксели, pan стрелками, бесконечный loop,
  подтверждение удаления только для нескольких файлов, native Open With,
  English UI, системная тема с чёрными полями, Supported formats only.
- 2026-10-31: создан скелет этапа 1 без проверок. Зависимости Hosting 10.0.0,
  Microsoft.NET.Test.Sdk 17.14.1, xUnit 2.9.3, runner 3.1.1 указаны, но restore
  не выполнялся. Декодеры намеренно не добавлены до исследования на этапе 2.
  Зафиксированы HKCU settings, цветная консоль publish и копирование в C:\tools.
- 2026-11-01: реализована основа и рабочий UI. 50 tests passed, 3 native
  integration tests skipped из-за ограничений sandbox. Финальный single-file
  EXE в artifacts/release/win-x64; smoke WebP/PNG прошёл. Требуется пользовательская
  приёмка Windows-интеграций, сетевых путей и нескольких реальных мониторов.

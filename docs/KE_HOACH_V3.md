# Kế hoạch v3

## 0. Phạm vi và nguyên tắc

**Mục tiêu:** tách logic khỏi giao diện để dễ bảo trì sau kỳ N2 ngày 06/12/2026; chỉ L6 được thay đổi chức năng.

Đã đọc `AGENTS.md`, `docs/TIEN_DO_V2.md`, `README.md` và code liên quan. Commit cuối lúc khảo sát: `651f643ad638163a431161cc090c0d69073b7007`. Các dẫn chứng dòng dưới đây thuộc code tại thời điểm khảo sát.

Đây là kế hoạch, **chưa sửa file, chưa chạy test/build, chưa commit**. Test/build có thể ghi `bin/obj`, nên không chạy trong lượt chỉ đọc này.

### Quy tắc giao việc

- Claude tạo sáu worktree từ **cùng một commit đã chốt**.
- Mỗi làn chỉ sửa/tạo các file được liệt kê. **Mọi file ngoài danh sách đều cấm sửa**, kể cả tài liệu tiến độ và test dùng chung.
- Core nhận dữ liệu thuần; không tham chiếu Avalonia, Data, `App`, SQLite hoặc hệ thống tệp.
- L1–L5 giữ nguyên chữ, màu, phím, thứ tự, thời điểm ghi dữ liệu và định dạng lưu.
- Giữ các constructor, thuộc tính và hàm `Test*` đang được `SelfTest` gọi.
- Không sửa `.csproj`, `Directory.Build.props`, `App.axaml.cs`, `MainWindow`, `Ui`, Shared, migration hoặc Importer. Các project dùng SDK và đã có tham chiếu cần thiết; thêm file `.cs` không cần sửa project.
- Không sửa/chép đè `data/`, không chạy Importer, `build.ps1` hoặc exe trong sandbox.

### Mốc kiểm chứng

`docs/TIEN_DO_V2.md` ghi **397 test xanh** trên môi trường có dữ liệu. `AGENTS.md` trên đĩa lúc đọc lại ghi mốc worktree thiếu dữ liệu là **154 xanh và 3 lỗi `ExamDataTests` “No data found”**. Không coi hai con số này là kết quả vừa chạy.

Trước khi triển khai, Claude ghi baseline thực tế cho từng môi trường:

1. Test trước sửa, ghi riêng lỗi do thiếu fixture.
2. Test sau sửa: không có hồi quy; toàn bộ test mới phải xanh.
3. Sau ghép, chạy đầy đủ trên môi trường có dữ liệu: tất cả xanh, build 0 lỗi.
4. Không dùng việc thiếu dữ liệu để bỏ qua lỗi assertion hoặc lỗi khác của `ExamDataTests`.

Mỗi shell đặt:

```powershell
$env:PATH = 'D:\tools\dotnet;D:\tools\git\cmd;' + $env:PATH
$env:DOTNET_ROOT = 'D:\tools\dotnet'
$env:NUGET_PACKAGES = 'D:\tools\nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
```

Khi assets đã restore:

```powershell
dotnet test "tests\Nihongo.Tests" --no-restore
dotnet build "frontend\Nihongo.App" --no-restore -v q
```

Không thêm package. Nếu worktree chưa có assets, Claude chuẩn bị restore offline trước khi giao việc.

## L1 — Tách Denken

**Mục tiêu:** tách danh sách, phiên luyện và phần thi/tổng kết; đưa lọc, chọn câu, thống kê và chấm sang Core.  
**Model:** `gpt-6.1-terra`.

### File được sửa/tạo

**Sửa:**

- `frontend/Nihongo.App/Features/Denken/DenkenView.cs`

**Tạo:**

- `frontend/Nihongo.App/Features/Denken/DenkenSessionView.cs`
- `frontend/Nihongo.App/Features/Denken/DenkenSessionView.Exam.cs`
- `frontend/Nihongo.App/Features/Denken/DenkenSessionView.Summary.cs`
- `frontend/Nihongo.App/Features/Denken/DenkenData.cs`
- `backend/Nihongo.Core/Quiz/DenkenSelection.cs`
- `backend/Nihongo.Core/Quiz/DenkenGrading.cs`
- `tests/Nihongo.Tests/DenkenSelectionTests.cs`
- `tests/Nihongo.Tests/DenkenGradingTests.cs`

**Cấm đáng chú ý:** `ContentStore.Extra.cs`, `ReviewPrompt.cs`, `AskGemini.cs`, `StatsView*`, `SelfTest*`, Shared.

### Thiết kế

Không chuyển `DenkenQuestion` sang Shared: model này đang ở Data và có `ToWrongItem()` trả về kiểu của Core — `backend/Nihongo.Data/Repositories/ContentStore.Extra.cs:8`, `:27`. Chuyển nguyên model sang Shared sẽ tạo phụ thuộc ngược.

Dùng bản ghi đầu vào thuần trong Core:

```csharp
public sealed record DenkenListItem(
    string SrsId, string Level, string Subject,
    string TopicCode, string TopicName,
    string YearLabel, int Year, int Qno, int Week);

public sealed record DenkenAnswer(
    int Choice, int Picked, bool? Self);

public static IReadOnlyList<DenkenListItem> Filter(
    IEnumerable<DenkenListItem> items, string level, string subject);

public static IReadOnlyList<string> Pick(
    IEnumerable<DenkenListItem> items,
    IReadOnlyDictionary<string, SrsEntry> srs,
    IReadOnlyDictionary<string, (int Correct, int Total)> attempts,
    int count, int masteredBox, Func<int> nextRandom);

public static bool IsGraded(
    DenkenAnswer answer, bool timed, bool submitted);

public static bool IsRight(DenkenAnswer answer);

public static DenkenGrade Grade(
    IReadOnlyList<DenkenAnswer> answers, bool timed, bool submitted);
```

`DenkenGrade` chứa `Correct`, `Graded`, `Pending`, `WrongIndexes`.

Trong `DenkenSelection.cs`, bổ sung các hàm gom nhóm chủ đề/năm/môn và tính tổng lượt đúng/tổng lượt làm; trả record chứa mã câu và số liệu, không trả control hoặc màu.

`DenkenData` ở App:

- Nạp `ContentStore`/`ProgressStore`, chụp các dictionary cần dùng.
- Ánh xạ `DenkenQuestion` → `DenkenListItem`.
- Tra mã trả về thành `DenkenQuestion`.
- Giữ nguyên cache và điểm truy cập `DenkenView.All`.

Dùng partial cho `DenkenSessionView`, không dựng hai lớp luyện/thi có state trùng nhau. Constructor hiện tại vẫn nhận `minutes`; `0` là luyện, `>0` là thi.

### Trình tự

1. Viết test khóa hành vi chọn/chấm hiện tại.
2. Tách nguyên lớp `DenkenSessionView` sang file riêng, chưa đổi thân hàm.
3. Tách `Submit`, `Score` sang `.Exam.cs`; tổng kết, prompt, làm lại câu sai sang `.Summary.cs`.
4. Đưa biểu thức lọc/gom nhóm/chọn câu sang Core; giữ các điều kiện và thứ tự sắp xếp.
5. Đưa `Graded`, `IsRight`, `WrongIndexes` sang `DenkenGrading`.
6. Thay truy vấn tiến độ lặp trong vòng lặp bằng snapshot qua `DenkenData`; không thay thời điểm refresh.
7. Đối chiếu các lần gọi `Study.Answer`, `LogWrong`, `SaveResult` trước/sau.

### Test thêm

`DenkenSelectionTests`:

- Lọc cấp và môn; môn rỗng lấy toàn cấp.
- Chủ đề giữ thứ tự `Week`, rồi `TopicCode`.
- Đề năm giữ thứ tự năm/nhãn năm giảm dần, câu tăng theo `Qno`.
- Ưu tiên yếu → chưa làm → đã làm; hòa dùng nguồn random truyền vào.
- Ngưỡng `MasteredBox`; số lượng yêu cầu lớn hơn kho.
- Thống kê môn dùng toàn cấp; chủ đề yếu dùng bộ lọc môn hiện hành.

`DenkenGradingTests`:

- Luyện: câu bỏ trống không vào mẫu số.
- Thi đã nộp: câu trắc nghiệm bỏ trống tính sai.
- Câu tự chấm `null/true/false`.
- Danh sách rỗng; danh sách câu sai giữ thứ tự.
- Kết quả thi lưu chỉ phần tự động, khác tổng kết có tự chấm.

### Tiêu chí đạt

- File gốc từ **28.739 byte** còn dưới **14 KiB**; mỗi file view mới dưới **14 KiB**.
- Core không phụ thuộc Data/App.
- Giữ `DenkenView.All`, constructor và các hàm `Test*`.
- Kịch bản hiện có cho kết quả luyện **2/4**, hai câu sai; không thành 2/5.
- Test/build đạt quy tắc chung.

### Bẫy hiện tại

- `DenkenView.cs:153`: nhóm chủ đề yếu theo `TopicName`, không theo mã; không tự “sửa” khóa nhóm.
- `:163`: lịch sử thi lấy mọi cấp, không lọc `_level`.
- `:294`: câu bỏ trống được tính khác nhau giữa luyện và thi.
- `:385`: `Recorded` chống ghi lần trả lời lặp; đây là state phiên, không bỏ khi tách.
- `:489`: thi chỉ ghi `Study.Answer` cho câu đã chọn, dù bỏ trống vẫn nằm trong mẫu số kết quả.
- `:495`: kết quả thi ban đầu chỉ tính câu trắc nghiệm; tự chấm sau không cập nhật kết quả đã lưu.
- `:314`: `Finish()` hiện không chống chốt nhiều lần. Ghi nhận, không tự đổi trong refactor.
- `:230`, `:247`: giữ sự kiện WebView, dừng timer và hủy tự chuyển.

## L2 — Tách nghiệp vụ thi thử

**Mục tiêu:** đưa xử lý nháp, chấm bài và thu câu sai ra khỏi `ExamView`; giữ nguyên giao diện và tác dụng phụ.  
**Model:** `gpt-6.1-terra`.

### File được sửa/tạo

**Sửa:**

- `frontend/Nihongo.App/Features/Exam/ExamView.cs`

**Tạo:**

- `frontend/Nihongo.App/Features/Exam/ExamView.Layout.cs`
- `frontend/Nihongo.App/Features/Exam/ExamView.Question.cs`
- `frontend/Nihongo.App/Features/Exam/ExamView.Media.cs`
- `frontend/Nihongo.App/Features/Exam/ExamView.Result.cs`
- `frontend/Nihongo.App/Features/Exam/ExamSubmissionWriter.cs`
- `backend/Nihongo.Core/Quiz/ExamDraftCodec.cs`
- `backend/Nihongo.Core/Quiz/ExamSubmission.cs`
- `tests/Nihongo.Tests/ExamDraftCodecTests.cs`
- `tests/Nihongo.Tests/ExamSubmissionTests.cs`

**Cấm đáng chú ý:** `ExamCatalog*`, `ExamScoring.cs`, `J2Stats.cs`, `AskGemini.cs`, `ReviewPrompt.cs`, `SelfTest*`.

### Thiết kế

Giữ `ExamDef`/`ExamItem` trong App. Chúng đang khai báo tại `frontend/Nihongo.App/Services/ExamCatalog.cs:10`, `:17`.

Core nhận:

```csharp
public sealed record ExamSubmissionItem(
    int Index, int Mondai, int No, int Answer,
    Question? Question, string? PdfAttemptId, string Label);

public static ExamSubmissionResult Evaluate(
    string examId, string level,
    IReadOnlyList<ExamSubmissionItem> items,
    IReadOnlyDictionary<int, int> picked);

public static ExamDraftState? Decode(
    string text, int totalSeconds, int itemCount);

public static string Encode(ExamDraftState state);
```

- `ExamDraftState`: `Remaining`, `Current`, `Picked`, `Flags`; `UsedBefore` suy từ tổng thời gian.
- `ExamSubmissionResult`: `ExamScore`, danh sách lần trả lời cần ghi, câu sai cần lưu, thống kê từng nhóm.
- Một lần trả lời chứa `ItemId`, `Correct`, cờ vào SRS hay chỉ log.
- Câu sai chứa `ItemId`, `Picked`, `Question?`, `Label`; PDF vẫn có bản ghi sai nhưng không có prompt trắc nghiệm.

`Evaluate` gọi **`ExamScore.From` hiện có**, không viết bộ quy đổi điểm mới.

`ExamSubmissionWriter` ở App nhận kết quả thuần, gọi `Study.Answer`/`Study.Log`, `LogWrong`, `SaveResult`. Chuyển câu sai có `Question` thành prompt bằng API `AskGemini.From` hiện có.

### Trình tự

1. Viết test nháp và kết quả từ các trường hợp hiện tại.
2. Tách layout, câu hỏi/phiếu trả lời, media và kết quả thành partial.
3. Di chuyển codec nháp nguyên định dạng `remain|current|picked|flags`.
4. Ánh xạ mỗi `ExamItem` một lần; `PdfAttemptId` lấy từ `J2Stats.PdfItemId`.
5. Thay logic trong `Submit` bằng `Evaluate` rồi ghi qua adapter.
6. Giữ timer, xác nhận nộp hai lần, điều hướng, lưu nháp và tự chuyển ở App.
7. Đối chiếu thứ tự xóa nháp → ghi attempt → ghi wrong → lưu result.

### Test thêm

`ExamDraftCodecTests`:

- Round-trip nháp đầy đủ, nháp rỗng, dưới bốn phần.
- Clamp thời gian tối thiểu 30 giây và vị trí câu.
- Bỏ mục không parse được theo đúng hành vi cũ.
- Khóa hành vi với chỉ số âm, phương án ngoài miền và số đầu vào lỗi; không âm thầm tăng validation.

`ExamSubmissionTests`:

- Đúng/sai/bỏ trống; bỏ trống vẫn ghi attempt nhưng không vào `wrong`.
- Câu có chữ vào SRS; PDF chỉ log nếu có mã.
- PDF sai có `wrong`, không có `Question` để dựng prompt.
- N2/N1 đúng ranh giới Mondai; nghe `>100`.
- Giữ `ExamScore` cho đề thiếu phần và J.TEST.
- Mã sai PDF đúng dạng `{examId}:m{mondai}:q{no}`.
- Hàm thuần gọi lặp không sửa đầu vào.

### Tiêu chí đạt

- `ExamView.cs` từ **35.015 byte** còn dưới **12 KiB**; mỗi partial dưới **14 KiB**.
- Không còn parse nháp, vòng chấm hoặc quyết định câu sai trong view.
- Giữ `HasDraft`, constructor, `Test*`, phím và các khóa setting.
- Test/build đạt quy tắc chung.

### Bẫy hiện tại

- `ExamView.cs:107`: `int.Parse` có thể ném lỗi; `:110` chỉ kiểm cận trên. Chống nháp hỏng là việc riêng, không gộp refactor.
- `:178`: khi về một cột phải đưa control ẩn về ô 0; đã có lịch sử crash Grid.
- `:484`: gọi Submit sau nộp là về danh sách, không chỉ `return`.
- `:499`: bỏ trống bị chấm sai và vẫn đi qua luồng ghi attempt.
- `:531`: bỏ trống bị loại khỏi bảng `wrong`.
- `:527`: mã phiên và thời gian dùng giờ máy; không đổi định dạng.
- `frontend/Nihongo.App/Services/J2Stats.cs:25`: PDF đọc dùng ngưỡng Mondai 10, chưa bao phủ đúng toàn bộ N1. Giữ nguyên và ghi việc sau.
- `ExamView.cs:566`: J.TEST có nhánh hiển thị riêng; không áp điều kiện đỗ JLPT cho nhánh này.

## L3 — Tách số liệu Hôm nay và Tiến độ

**Mục tiêu:** đưa tính số liệu của ba file view sang các hàm thuần, để App chỉ nạp snapshot và vẽ.  
**Model:** `gpt-6.1-terra`.

### File được sửa/tạo

**Sửa:**

- `frontend/Nihongo.App/Pages/TodayView.cs`
- `frontend/Nihongo.App/Pages/StatsView.cs`
- `frontend/Nihongo.App/Pages/StatsView.Overview.cs`

**Tạo:**

- `frontend/Nihongo.App/Services/DashboardData.cs`
- `backend/Nihongo.Core/Stats/TodayMetrics.cs`
- `backend/Nihongo.Core/Stats/StatsMetrics.cs`
- `backend/Nihongo.Core/Stats/StatsOverviewMetrics.cs`
- `tests/Nihongo.Tests/TodayMetricsTests.cs`
- `tests/Nihongo.Tests/StatsMetricsTests.cs`
- `tests/Nihongo.Tests/StatsOverviewMetricsTests.cs`

**Cấm đáng chú ý:** `StudyStats.cs`, `PassPredictor.cs`, `SessionPlanner.cs`, `PlannerInputBuilder.cs`, `J2Stats.cs`, `DenkenView*`, `Study.cs`.

### Thiết kế

Các API thuần chính:

```csharp
public static DateOnly ResolveExamDay(string setting, DateOnly today);

public static IReadOnlyList<HeatmapDay> Heatmap(
    DateOnly today, int goal, IReadOnlyDictionary<DateOnly, int> xp);

public static string ReadingHook(string bodyJa);

public static WeekMetrics Week(
    IReadOnlyList<AttemptRow> attempts,
    IReadOnlyList<ExamResult> results,
    DateOnly today, int weekOffset,
    Func<string, int?> mondaiOf, IReadOnlyList<string> sections);

public static IReadOnlyList<string> Advice(
    JlptPrediction prediction,
    IReadOnlyList<AttemptRow> attempts,
    IReadOnlyList<ExamResult> results,
    DateTime now, Func<string, int?> mondaiOf);

public static IReadOnlyList<WeaknessMetric> Weaknesses(
    IReadOnlyList<Question> questions,
    IReadOnlyDictionary<string, (int Correct, int Total)> attempts);

public static IReadOnlyList<Question> PickPractice(
    IReadOnlyList<Question> pool,
    IReadOnlyDictionary<string, (int Correct, int Total)> attempts,
    int count, Func<int> nextRandom);
```

Các record đặt cùng file chức năng:

- `HeatmapDay`: ngày, XP, tương lai, hôm nay, mức tô theo mục tiêu.
- `WeekMetrics`: ngày bắt đầu, hai dãy `DayStat`, số câu/đúng hai tuần, chênh lệch tỷ lệ nullable, streak, phút thi.
- `WeaknessMetric`: loại, pool câu, sai/tổng lượt, cờ yếu nhất.
- Record tiến độ: số đã làm/tổng cho từ N2, đề, nghe, đọc, ngữ pháp.
- Record sưu tầm: nhóm, tổng bài, danh sách bài đã đọc, tổng toàn kho.
- Evidence Denken: nhóm theo môn từ dictionary mã câu → môn, attempts và results; tiếp tục gọi `PassPredictor.Denken`.

`DashboardData` nạp dữ liệu App/Data và chuẩn bị dictionary, danh sách ID. Không đưa `ExamDef` hoặc `DenkenQuestion` vào Core.

### Trình tự

1. Khóa đầu ra hiện tại bằng fixture nhỏ và thời gian cố định.
2. Tách ngày thi, hook bài đọc, 84 ô heatmap.
3. Tách số liệu tuần, độ chính xác theo phần và lời khuyên; gọi lại `StudyStats`.
4. Tách tiến độ, sưu tầm, điểm yếu và chọn câu luyện.
5. Tách evidence Denken; giữ công thức trong `PassPredictor`.
6. Gom đọc store tại `DashboardData`; view gọi hàm tính rồi vẽ.
7. Giữ `TodayView.ExamDay`, `LaterExamDays`, `ExamLevel`, `Label` và `StatsView.PracticeForTest` làm wrapper tương thích.

### Test thêm

`TodayMetricsTests`:

- Ngày thi hợp lệ, quá hạn, sai định dạng, đúng hôm nay.
- Heatmap 12×7 bắt đầu thứ Hai, có ô tương lai, XP bằng 0/vượt mục tiêu.
- Hook theo dấu `。`, mốc 80/90 ký tự và markup bài đọc.

`StatsMetricsTests`:

- Tuần trống, tuần trước trống, đổi tuần qua năm.
- Biên 7/14/30 ngày; streak còn tính hôm qua.
- Phút thi cộng giây rồi chia 60.
- Thứ tự lời khuyên; ngưỡng tối thiểu số lượt.
- Evidence Denken từ luyện và thi dùng trọng số hiện tại; môn đã đỗ.

`StatsOverviewMetricsTests`:

- Tổng tiến độ đối chiếu đúng các ID/tiền tố hiện tại.
- Sưu tầm gồm các cấp, không tự lọc theo `exam_level`.
- Điểm yếu không có dữ liệu, hòa tỷ lệ, câu không có options.
- Chọn luyện ưu tiên chưa làm, rồi tỷ lệ đúng thấp, rồi random.

### Tiêu chí đạt

- Ba view không còn trực tiếp `App.Content`/`App.Progress`; đọc/ghi qua adapter.
- Không còn tính tổng nghiệp vụ trong vòng dựng control; vòng vẽ vẫn được phép.
- Không sửa `StudyStats`/`PassPredictor`.
- Snapshot số liệu và nguyên văn lời khuyên không đổi.
- Test/build đạt quy tắc chung.

### Bẫy hiện tại

- `TodayView.cs:39`, `:45`: input và plan được cache theo vòng đời view; không tự refresh mỗi lần vẽ.
- `:180`: heatmap bắt đầu thứ Hai của 11 tuần trước; không phải hôm nay trừ 84 ngày.
- `:232`: resume dùng cửa sổ thời gian bắt đầu trừ một giây.
- `StatsView.cs:115`: phút thi không lọc theo cấp/tab; giữ nguyên.
- `:178`, `:188`: tiến độ từ đang cố định N2, ngữ pháp cố định Mondai 7. Đây là nợ N1, không sửa trong L3.
- `StatsView.Overview.cs:70`: sưu tầm lấy toàn kho.
- `:122`: phân nhóm dùng overload `SessionPlanner.Categorize(q)` hiện có; L6 phải giữ overload này không đổi.
- `:160`: luyện điểm yếu dùng random; không thay bằng cách chọn cố định.

## L4 — Tách repository, giữ facade ProgressStore

**Mục tiêu:** tách SQL theo bảng/nhóm bảng mà không thay API công khai, schema hoặc hành vi lưu.  
**Model:** `gpt-6.1-terra`.

### File được sửa/tạo

**Sửa:**

- `backend/Nihongo.Data/Repositories/ProgressStore.cs`

**Tạo:**

- `backend/Nihongo.Data/Repositories/Progress/ProgressDb.cs`
- `backend/Nihongo.Data/Repositories/Progress/SrsRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/AttemptRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/SeenRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/ResultRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/NotebookRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/TranslationRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/MediaRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/SettingRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/WatchRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/FollowRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/WrongRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/ExplainRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/ArticleReadRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/SessionRepository.cs`
- `backend/Nihongo.Data/Repositories/Progress/XpRepository.cs`
- `tests/Nihongo.Tests/Data/ProgressFacadeTests.cs`
- `tests/Nihongo.Tests/Data/ProgressRepositoryTests.cs`

**Cấm đáng chú ý:** migration, schema, `Migrator.cs`, Shared, mọi caller của `ProgressStore`, test cũ.

### Thiết kế

- `ProgressStore` vẫn là `public sealed class`; giữ **toàn bộ chữ ký**, default argument, `Intervals`, `MasteredBox`.
- Repository là `internal sealed class`; constructor nhận `ProgressDb`.
- Mỗi hàm chuyển sang repo giữ nguyên tham số và kiểu trả về; facade chỉ chuyển tiếp.
- Cache `_srs` chỉ có **một chủ sở hữu**, là `SrsRepository`.

```csharp
internal sealed class ProgressDb
{
    internal ProgressDb(string connectionString);
    internal SqliteConnection Open();
    internal static void Exec(
        SqliteConnection connection, string sql,
        params (string, object)[] args);
}
```

`AttemptRepository` và `SeenRepository` có overload nội bộ nhận connection đang mở:

```csharp
internal void Insert(
    SqliteConnection connection, string id, bool correct, DateTime at);

internal void Add(SqliteConnection connection, string id);
```

`SrsRepository.Review/ReviewV2` gọi hai repo này trên connection hiện tại. Không thêm transaction, không đổi pooling hay thời điểm cập nhật cache.

Giữ `ApplyPendingRestore`, trình tự constructor và `Backup` ở facade trong đợt này.

### Trình tự

1. Ghi danh sách public API hiện tại; thêm test facade trên DB tạm.
2. Tạo `ProgressDb`; giữ nguyên connection string.
3. Chuyển các bảng độc lập trước: setting, translation, notebook, media, follow, watch.
4. Chuyển result, wrong, explain, article_read, session, xp_log.
5. Chuyển attempt/seen, sau cùng mới chuyển SRS và cache.
6. Giữ trình tự restore → migrator → nạp cache.
7. Soát SQL trước/sau, thứ tự kết quả và tất cả default argument.
8. Chạy test cũ cùng test mới; kiểm tra DB sau khi mở lại và sau backup.

### Test thêm

`ProgressFacadeTests`:

- Các API cũ vẫn gọi được; `Intervals` và `MasteredBox` không đổi.
- `Review`/`ReviewV2` ghi attempt/seen như cũ.
- Cache và dữ liệu mở lại thống nhất; `SetReason` không mất ease/interval.
- Restore chờ và backup chỉ dùng thư mục tạm.

`ProgressRepositoryTests` kiểm qua facade:

- Round-trip notebook, translation, media, setting, follow.
- Watch tự hoàn tất 90%, giữ done, force done, duration bằng 0.
- `Wrongs` cùng giây giữ `rowid DESC`; lọc theo session.
- Results mới trước; attempt cũ trước và biên `from`.
- Ghi bài đọc nhiều lần, `ReadArticleIds` trả tập duy nhất.
- Session giữ JSON mảng bài đọc; XP rỗng trả 0 và nhóm đúng ngày.

### Tiêu chí đạt

- `ProgressStore.cs` từ **21.499 byte** còn dưới **9 KiB**.
- SQL bảng nghiệp vụ nằm trong repo; facade chỉ giữ khởi tạo, điều phối restore/backup và chuyển tiếp.
- Không sửa caller, không migration, không thêm API công khai.
- Test DB chỉ sử dụng thư mục tạm; test/build đạt quy tắc chung.

### Bẫy hiện tại

- `ProgressStore.cs:20`: restore có di chuyển DB và xóa sidecar; tuyệt đối không test với DB thật.
- `:74`, `:78`: câu đúng lần đầu vẫn ghi attempt/seen trước khi trả `null`.
- `:81`, `:349`: cache được cập nhật trước SQL; không đổi ngữ nghĩa lỗi trong refactor.
- `:93`: đổi reason phải giữ ease và interval.
- `:244`: quy tắc cập nhật watch không tương đương một phép gán `done`.
- `:296`: cùng timestamp phải giữ thứ tự rowid.
- `:347`: `ReviewV2` gọi `SrsStateOf` bằng connection riêng; không tiện tay đổi transaction.
- `:381`: tiếp tục dùng `CoreJson.Default.StringArray`.

## L5 — Chia SelfTest theo kịch bản

**Mục tiêu:** chia file lớn bằng partial, giữ nguyên toàn bộ kịch bản và cách khởi chạy.  
**Model:** `gpt-6-luna`.

### File được sửa/tạo

**Sửa:**

- `frontend/Nihongo.App/Services/SelfTest.cs`

**Tạo:**

- `frontend/Nihongo.App/Services/SelfTest.Ui.cs`
- `frontend/Nihongo.App/Services/SelfTest.Sizes.cs`
- `frontend/Nihongo.App/Services/SelfTest.Review.cs`
- `frontend/Nihongo.App/Services/SelfTest.Keys.cs`
- `frontend/Nihongo.App/Services/SelfTest.Denken.cs`
- `frontend/Nihongo.App/Services/SelfTest.Stats.cs`
- `frontend/Nihongo.App/Services/SelfTest.Session.cs`
- `frontend/Nihongo.App/Services/SelfTest.Reading.cs`
- `frontend/Nihongo.App/Services/SelfTest.Appearance.cs`
- `frontend/Nihongo.App/Services/SelfTest.Helpers.cs`

**Cấm đáng chú ý:** mọi view, `MainWindow`, `App.axaml.cs`, `Program.cs`, `.csproj`, test project.

### Kiểu/hàm

Tất cả file khai báo:

```csharp
static partial class SelfTest
```

Không tạo API mới. Giữ chữ ký:

```csharp
public static bool Enabled { get; }
public static async void Run(MainWindow w);
```

Các kịch bản giữ dạng:

```csharp
static Task Scenario(MainWindow w, string dir, List<string> log);
```

Tên hàm thực tế không đổi.

### Trình tự

1. File gốc giữ `Mode`, `Enabled`, `Run` và xử lý log/đóng cửa sổ.
2. Chuyển:
   - `Ui` → `.Ui.cs`.
   - `Sizes`, `RespSizes` → `.Sizes.cs`.
   - `Review` → `.Review.cs`.
   - `Keys` → `.Keys.cs`.
   - `Extra`, `Denken` → `.Denken.cs`.
   - `Stats`, `ProgressMode` → `.Stats.cs`.
   - `Session`, `RunSession` → `.Session.cs`.
   - `ReadMode` → `.Reading.cs`.
   - `FontMode`, `GeminiMode` → `.Appearance.cs`.
   - `Shot`, `Press` → `.Helpers.cs`.
3. Chỉ bổ sung `using` cần thiết; không sửa thân hàm, delay, tên ảnh hoặc dòng log.
4. Soát diff để đảm bảo mỗi hàm xuất hiện đúng một lần.
5. Build, rồi bàn giao danh sách kịch bản cho kiểm tra sau ghép.

### Kiểm thử

**Không thêm lớp xUnit** cho việc di chuyển nguyên thân hàm: test project không tham chiếu App, và thêm test kiểm chuỗi mã nguồn không chứng minh hành vi UI.

Kiểm chứng bắt buộc:

- Giữ đủ 12 mode: `ui`, `extra`, `sizes`, `review`, `keys`, `stats`, `denken`, `read`, `session`, `progress`, `font`, `gemini`.
- Không mất lời gọi `RespSizes`.
- Giữ tên file ảnh, `selftest.log`, đường dẫn đầu ra và đóng cửa sổ khi xong.
- Sau ghép, người kiểm thử chạy các mode liên quan trên bản sao dữ liệu, ngoài sandbox.

### Tiêu chí đạt

- `SelfTest.cs` từ **37.552 byte** còn dưới **4 KiB**.
- Mỗi partial dưới **12 KiB**.
- Không thay đổi thân kịch bản ngoài định dạng/using.
- Không đổi số test xUnit do L5; build 0 lỗi.
- Báo rõ smoke UI chưa chạy nếu môi trường không cho mở app.

### Bẫy hiện tại

- `SelfTest.cs:20`: danh sách mode nhiều hơn comment đầu file.
- `:43`, `:44`: log được ghi sau catch rồi đóng cửa sổ.
- `:276`, `:388`, `:472`: nhiều mode ghi tiến độ, không chỉ mode `ui`.
- `:351`, `:487`: phụ thuộc constructor/hook mà L1/L3 phải giữ.
- `:550`: ảnh dùng 96 DPI có chủ ý; không đổi theo scaling màn hình.

## L6 — Tổng kết từ sai và lập buổi theo cấp

**Mục tiêu:** bổ sung từ vựng sai vào prompt tổng kết và lập buổi N1/N2 đúng cấp, không sửa hành vi các màn luyện độc lập.  
**Model:** `gpt-6.1-terra`; không giao Luna vì có tương thích nháp và phân loại N1.

### Dữ liệu đã kiểm tra

Truy vấn `data/noi_dung.db` bằng kết nối SQLite **read-only** ngày 16/11/2026:

| Dữ liệu N1 | Kết quả |
|---|---:|
| Bài đọc hiểu | 100 |
| Đoạn văn mẫu | 8 |
| Câu hỏi bài đọc | 300 |
| Mục từ vựng có `level=N1` | 7.214 |
| Câu hỏi nối được `question.source_title = exam.id`, `exam.level=N1` | 1.687 |
| Trong đó Mondai 5–7 | 432 |
| Bộ nghe `listen-real-n1-*` | 8 |
| MP3 và PDF đề của tám bộ nghe tồn tại | 8/8 |

Có đủ dữ liệu để lập buổi N1. “Tồn tại file” chưa chứng minh phát được audio hoặc nội dung không lỗi. Bản phát hành mở chỉ có bài đọc theo `TIEN_DO_V2.md`, nên vẫn phải xử lý kho thiếu từ/câu/nghe.

### File được sửa/tạo

**Sửa:**

- `frontend/Nihongo.App/Features/Session/SessionView.cs`
- `frontend/Nihongo.App/Features/Session/SessionDraft.cs`
- `frontend/Nihongo.App/Features/Session/PlannerInputBuilder.cs`
- `frontend/Nihongo.App/Services/AskGemini.cs`
- `backend/Nihongo.Core/Quiz/ReviewPrompt.cs`
- `backend/Nihongo.Core/Services/SessionPlanner.cs`

**Tạo:**

- `backend/Nihongo.Core/Quiz/SessionReview.cs`
- `backend/Nihongo.Core/Services/SessionLevelPolicy.cs`
- `tests/Nihongo.Tests/SessionReviewTests.cs`
- `tests/Nihongo.Tests/SessionLevelPolicyTests.cs`
- `tests/Nihongo.Tests/SessionPlannerLevelTests.cs`

**Cấm đáng chú ý:** `TodayView*`, `StatsView*`, `SelfTest*`, `ProgressStore*`, `ExamCatalog*`, `J2Stats.cs`, các view Vocab/Quiz/Listening, Shared và test cũ.

### Thiết kế

#### A. Tổng kết câu/từ sai

```csharp
public sealed record SessionReviewData(
    IReadOnlyList<WrongEntry> Questions,
    IReadOnlyList<VocabItem> Words);

public static SessionReviewData Collect(
    DateTime started, DateTime ended,
    IReadOnlyList<AttemptRow> attempts,
    IReadOnlyList<WrongEntry> wrongs,
    IReadOnlyDictionary<string, VocabItem> cards,
    IReadOnlySet<string> reviewIds);

public static string VocabPrompt(
    string title,
    IEnumerable<(string Word, string Reading, string Meaning)> words,
    string level);

public static IReadOnlyList<string> SessionPrompts(
    string title, string level,
    IEnumerable<WrongItem> questions,
    IEnumerable<VocabItem> words);
```

- Giữ overload `VocabPrompt(title, words)` cũ, ủy quyền với `"N2"`.
- `SessionPrompts` ở `AskGemini`; không đổi bộ lọc của `Prompts` cũ.
- Câu hỏi lấy từ `wrong`, mới nhất mỗi ID; từ vựng lấy attempt sai, tra bằng `CardId`.
- Một từ sai rồi đúng lại vẫn thuộc danh sách cần chữa.
- Không dựng giả phương án đã chọn cho từ vựng: bảng attempt chỉ lưu đúng/sai.
- Giới hạn **40 mục tổng cộng**: câu hỏi trước, rồi từ vựng. Phần từ chia tối đa 10 từ/lời nhắc; giữ luồng nút copy nhiều phần.
- Chỉ lấy ID thuộc buổi và khoảng thời gian buổi, có dung sai timestamp như luồng cũ.

Nháp phải giữ tập ID gốc để resume không làm mất từ đã trả lời:

- Thêm trường tùy chọn `ReviewIds` và `ReviewLevel` vào `SessionDraft`.
- Nháp mới lưu tập ID gốc và cấp lúc bắt đầu.
- Nháp cũ thiếu trường: lấy ID từ `Steps`, cấp từ setting hiện tại.
- Khi save lại nháp, không thay tập gốc bằng danh sách đã lọc `Remaining`.
- Tiếp tục dùng `DraftJson` sinh mã sẵn; không đổi Shared `SessionPlan`.

#### B. Cấp N1/N2

```csharp
public static string Normalize(string level);

public static string? QuestionLevel(
    Question question, IReadOnlyDictionary<string, string> examLevels);

public static string ListeningLevel(ListeningSet set);

public static Weakness? Categorize(Question question, string level);
```

- `exam_level` hợp lệ N1/N2; giá trị khác về N2.
- Cấp câu mock lấy từ `LoadExams()` theo `SourceTitle`; không đoán từ câu chữ.
- Các nguồn cũ chưa có metadata phải được đối chiếu importer trước khi gán N2; nguồn không xác định không đưa vào pool N1.
- Bộ nghe theo quy ước đã dùng tại `ListeningListView.cs:12`; loại J.TEST khỏi buổi JLPT.
- Lọc `Questions` và `Listening` trong `PlannerInputBuilder`.
- Giữ review đến hạn của các cấp để không bỏ nợ SRS khi đổi kỳ thi; cấp chỉ giới hạn **bài đọc, câu luyện điểm yếu và nghe được chọn thêm**.
- Giữ `Categorize(Question)` cũ nguyên nghĩa N2. Chỉ planner gọi overload có cấp.
- N1: Mondai 5–7 là Grammar; 8–13 là đọc, không đưa vào Grammar. Không gán Mondai 3 thành Affix vì N1 không có 語形成. Với Mondai 3–4, dùng nhận diện hiragana/phó từ hiện có, còn lại Nuance.
- Giữ seed, thứ tự bước và công thức ngân sách hiện tại.

### Trình tự

1. Viết test collector từ attempt sai và wrong.
2. Thêm overload prompt có cấp, bảo đảm API cũ cho kết quả nguyên vẹn.
3. Bổ sung dữ liệu tổng kết vào `SessionView`, không sửa callback của view con.
4. Thêm metadata nháp tùy chọn, giữ ID/cấp qua nhiều lần resume.
5. Viết policy cấp và test; ánh xạ metadata trong builder.
6. Thêm phân loại N1 vào planner mà không đổi overload cũ.
7. Kiểm ba mức 15/30/45 phút với dữ liệu trộn cấp và kho thiếu nội dung.
8. Đối chiếu XP, attempt, result trước/sau: thay đổi prompt không được ghi thêm tiến độ.

### Test thêm

`SessionReviewTests`:

- Sai từ trong VocabQuiz và Flash được tìm qua `v:`/`CardId`.
- Sai rồi đúng, sai lặp, từ trong sổ tay, ID không tra được.
- Loại attempt ngoài thời gian và ngoài tập ID buổi.
- Câu sai lấy lựa chọn mới nhất; chỉ từ sai vẫn tạo prompt.
- Không sai thì danh sách prompt rỗng.
- Giới hạn 40; chia phần; N1 không còn lời mở đầu N2.
- Tập ID gốc được giữ khi danh sách bước còn lại giảm.

`SessionLevelPolicyTests`:

- Câu mock lấy cấp từ metadata, không từ tên hiển thị.
- N1/N2/J.TEST, nguồn không xác định.
- Bộ nghe đúng tiền tố; setting rỗng/sai.
- Overload prompt cũ không đổi nội dung.

`SessionPlannerLevelTests`:

- N1 Mondai 5–7 là ngữ pháp; 8/9 không bị nhận là ngữ pháp.
- N1 Mondai 3 không thành Affix.
- Cùng input/seed cho cùng plan; không chọn câu/bộ nghe khác cấp.
- Review đến hạn cấp cũ vẫn được giữ.
- N1 đọc 12 phút; các mức 15/30/45.
- Kho chỉ có bài đọc, không có nghe/câu hỏi, bài đã đọc hết.
- Không có bước chứa ID rỗng hoặc câu không có options.

### Tiêu chí đạt

- Tổng kết chỉ có từ sai vẫn bật nút Gemini và có nội dung.
- Prompt buổi N1 dùng cấp N1.
- API cũ của `AskGemini`, `ReviewPrompt`, `SessionPlanner.Categorize(q)` không đổi.
- Không thêm cột/bảng hoặc ghi thêm attempt/XP.
- Lập buổi N1 không lấy câu luyện mới/nghe N2 hoặc J.TEST.
- Test/build đạt quy tắc chung.

### Bẫy hiện tại

- `SessionView.cs:219`: tổng kết hiện chỉ tra `QuestionsById`, nên bỏ mất từ vựng.
- `:223`: prompt đang cố định `"N2"`.
- `VocabQuizView.cs:148`, `FlashView.cs:90`: từ sai được ghi qua `Study.Answer`, không qua `LogWrong`.
- `SessionDraft.cs:37`: `Remaining()` bỏ ID đã trả lời; không dùng plan còn lại làm tập ID gốc.
- `SessionPlanner.cs:65`: bài đọc đã lọc cấp.
- `:88`, `:146`: nghe và câu điểm yếu chưa lọc cấp.
- `:105`: phân loại ngữ pháp 7–9 hiện là N2.
- `shared/Nihongo.Shared/Models/Content.cs:27`, `:72`: `Question` và `ListeningSet` không có trường Level.
- `AskGemini.cs:95`: `Prompts` loại mục không có options; không sửa rộng hàm này vì L1/L2 và các màn khác đang dùng.
- `SessionView.cs:167`: tổng số câu hiện lấy mọi attempt từ giờ bắt đầu; sửa cách đếm tổng kết là việc riêng, không gộp vào bổ sung prompt.

## 7. Ma trận quyền sửa

Các file mới đã chia tên/thư mục riêng trong từng làn. Bảng dưới khóa các điểm dễ đụng nhau:

| File/nhóm hiện có | L1 | L2 | L3 | L4 | L5 | L6 |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| `Features/Denken/DenkenView.cs` | Sửa | — | Đọc | — | Đọc | — |
| `Features/Exam/ExamView.cs` | — | Sửa | — | — | Đọc | — |
| `Pages/TodayView.cs` | — | — | Sửa | — | Đọc | Đọc |
| `Pages/StatsView.cs`, `.Overview.cs` | — | — | Sửa | — | Đọc | — |
| `Repositories/ProgressStore.cs` | Đọc | Đọc | Đọc | Sửa | Đọc | Đọc |
| `Services/SelfTest.cs` | Đọc | Đọc | Đọc | — | Sửa | Đọc |
| `Features/Session/SessionView.cs` | — | — | Đọc | — | Đọc | Sửa |
| `Features/Session/SessionDraft.cs` | — | — | Đọc | — | — | Sửa |
| `Features/Session/PlannerInputBuilder.cs` | — | — | Đọc | — | Đọc | Sửa |
| `Services/AskGemini.cs` | Đọc | Đọc | — | — | Đọc | Sửa |
| `Core/Quiz/ReviewPrompt.cs` | Đọc | Đọc | — | — | — | Sửa |
| `Core/Services/SessionPlanner.cs` | — | — | Đọc | — | Đọc | Sửa |
| `ContentStore*`, Shared, `CoreJson` | Đọc | Đọc | Đọc | Đọc | — | Đọc |
| `ExamCatalog*`, `J2Stats`, `StudyStats`, `PassPredictor` | Đọc | Đọc | Đọc | — | Đọc | Đọc |
| `.csproj`, `App.axaml.cs`, `MainWindow`, `Ui`, `Program.cs` | Cấm | Cấm | Cấm | Cấm | Cấm | Cấm |
| Test cũ, docs chung, migration, Importer | Cấm | Cấm | Cấm | Cấm | Cấm | Cấm |

**Mỗi file hiện có có tối đa một làn được sửa.** Không làn nào cần code mới của làn khác để build trên commit gốc.

Nếu phát sinh nhu cầu sửa file chung ngoài bảng: ghi **“cần làm sau ghép”**, kèm caller và thay đổi cần thiết; không tự mở rộng phạm vi.

## 8. Thứ tự ghép và nghiệm thu

Đề xuất: **L4 → L1 → L2 → L3 → L5 → L6**.

1. L4 trước để xác nhận facade dữ liệu không đổi.
2. L1/L2 tiếp theo, kiểm lại lưu kết quả và các hook.
3. L3 sau đó, xác nhận số liệu từ facade mới.
4. L5 kiểm biên dịch toàn bộ hook sau khi tách view.
5. L6 cuối vì đây là làn duy nhất thay chức năng.

Sau mỗi lần ghép:

- Kiểm diff chỉ nằm trong whitelist.
- Chạy test và build; không nhận hồi quy.
- Ghi số test trước/sau, lỗi thiếu fixture nếu có, kiểm chứng chưa chạy và rủi ro.
- Không push, không rebase hoặc sửa lịch sử.

Quy định commit trong nội dung AGENTS người dùng cung cấp và file trên đĩa đang khác nhau. Với lượt lập kế hoạch này không có commit. Khi triển khai, Claude chốt quyền ghi Git theo môi trường; agent bị chặn `.git` bàn giao diff để Claude commit sau duyệt, không tự vượt sandbox.

Sau khi ghép đủ:

- Chạy toàn bộ suite trên môi trường có dữ liệu.
- Kiểm UI ngoài sandbox trên bản sao dữ liệu: `denken`, `review`, `keys`, `stats`, `progress`, `session`, `sizes`.
- Kiểm thêm tổng kết chỉ sai từ, resume nhiều lần và buổi N1.
- Đối chiếu ảnh/chữ/phím L1–L5; kiểm XP và số bản ghi không tăng ngoài dự kiến.
- Chỉ kết luận hoàn thành khi phân biệt rõ test tự động đã chạy và smoke UI đã chạy.

## 9. Cố ý để sau

- Sửa nháp thi hỏng, chỉ số âm và validation phương án.
- Chống ghi kết quả lặp khi chốt Denken nhiều lần; điều chỉnh việc tính câu trống/tự chấm.
- Chuẩn hóa Level cho toàn bộ model, schema và nguồn dữ liệu.
- Sửa thống kê N1 toàn diện: từ N2 cố định, Mondai ngữ pháp, mã PDF, scope lịch sử và dự đoán.
- Gắn session ID vào attempt để xác định buổi tuyệt đối; hiện vẫn có giới hạn do timestamp lưu tới giây.
- Thu đáp án sai chi tiết của nghe/bài đọc vào tổng kết buổi; hiện không đủ dữ liệu lựa chọn trong attempt.
- Tách MP3 theo câu, xác minh phát audio và chất lượng nội dung N1.
- Nâng cấp giao diện, framework, package, công thức XP/SRS hoặc chính sách ngân sách buổi học.
- Sửa tài liệu v2 còn trạng thái cũ và đóng gói bản phát hành; Claude làm sau ghép.
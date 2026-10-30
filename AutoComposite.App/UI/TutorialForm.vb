Imports System.IO
Imports AutoComposite

''' <summary>教學的一個步驟。</summary>
Friend NotInheritable Class TutorialStep
    Public Property Title As String = ""
    ''' <summary>說明文字（換行用 vbCrLf）。</summary>
    Public Property Body As String = ""
    ''' <summary>Tutorial\ 資料夾裡的示意圖。</summary>
    Public Property Image As String = ""
    Public Property ImageCaption As String = ""
    ''' <summary>步驟按鈕：文字與動作（直接操作主視窗）。</summary>
    Public Property Actions As New List(Of (Text As String, Run As Action))()
    ''' <summary>完成條件；Nothing 表示純說明，隨時可以下一步。</summary>
    Public Property IsDone As Func(Of Boolean)
End Class

''' <summary>
''' 引導式教學：貼在主視窗右側的小視窗，一步一步帶使用者完成第一張合成。
''' 每一步有說明、示意圖、可以直接按的操作按鈕，並自動偵測這一步完成了沒有（✔）。
''' 範例圖片（紫藤花隧道背景、賽博龐克半身像人物）放在程式資料夾的 Tutorial\。
''' </summary>
Friend NotInheritable Class TutorialForm
    Inherits Form

    Private ReadOnly _main As MainForm
    Private ReadOnly _steps As List(Of TutorialStep)
    Private _index As Integer

    Private ReadOnly _progress As New Label() With {.Dock = DockStyle.Top, .Height = 22, .ForeColor = SystemColors.GrayText}
    Private ReadOnly _title As New Label() With {.Dock = DockStyle.Top, .Height = 34, .Font = New Font("Microsoft JhengHei UI", 13, FontStyle.Bold)}
    Private ReadOnly _status As New Label() With {.Dock = DockStyle.Top, .Height = 24}
    Private ReadOnly _picture As New PictureBox() With {.Dock = DockStyle.Top, .Height = 230, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.FromArgb(40, 40, 44), .Cursor = Cursors.Hand}
    Private ReadOnly _caption As New Label() With {.Dock = DockStyle.Top, .Height = 34, .ForeColor = SystemColors.GrayText}
    Private ReadOnly _body As New TextBox() With {.Dock = DockStyle.Fill, .Multiline = True, .ReadOnly = True, .BorderStyle = BorderStyle.None, .ScrollBars = ScrollBars.Vertical, .TabStop = False}
    Private ReadOnly _actions As New FlowLayoutPanel() With {.Dock = DockStyle.Bottom, .Height = 76, .WrapContents = True, .Padding = New Padding(0, 4, 0, 0)}
    Private ReadOnly _prev As New Button() With {.Text = "◀ 上一步", .Width = 100, .Height = 30}
    Private ReadOnly _next As New Button() With {.Text = "下一步 ▶", .Width = 100, .Height = 30}
    Private ReadOnly _timer As New Timer() With {.Interval = 500}

    Public Sub New(main As MainForm)
        _main = main
        Text = "使用教學"
        Width = 460 : Height = 820
        FormBorderStyle = FormBorderStyle.SizableToolWindow
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
        Font = New Font("Microsoft JhengHei UI", 9.5F)
        Padding = New Padding(12)
        _body.BackColor = BackColor

        Dim nav As New FlowLayoutPanel() With {.Dock = DockStyle.Bottom, .Height = 40, .FlowDirection = FlowDirection.RightToLeft}
        Dim closeButton As New Button() With {.Text = "關閉", .Width = 80, .Height = 30}
        AddHandler closeButton.Click, Sub() Close()
        nav.Controls.AddRange({_next, _prev, closeButton})
        AddHandler _prev.Click, Sub() ShowStep(_index - 1)
        AddHandler _next.Click, Sub()
                                    If _index = _steps.Count - 1 Then Close() Else ShowStep(_index + 1)
                                End Sub
        AddHandler _picture.Click, Sub() ShowLargeImage()

        Controls.Add(_body)
        Controls.Add(_caption)
        Controls.Add(_picture)
        Controls.Add(_status)
        Controls.Add(_title)
        Controls.Add(_progress)
        Controls.Add(_actions)
        Controls.Add(nav)

        _steps = BuildSteps()
        AddHandler _timer.Tick, Sub() UpdateStatus()
        _timer.Start()
        ShowStep(0)
    End Sub

    ''' <summary>放在主視窗右側（螢幕放不下時疊在主視窗右上角內側）。</summary>
    Public Sub PlaceBesideOwner()
        Dim area = Screen.FromControl(_main).WorkingArea
        Dim x = _main.Right
        If x + Width > area.Right Then x = _main.Right - Width - 20
        Location = New Point(Math.Max(area.Left, x), Math.Max(area.Top, _main.Top + 80))
    End Sub

    Private Shared Function TutorialFile(name As String) As String
        Return Path.Combine(AppContext.BaseDirectory, "Tutorial", name)
    End Function

    Private ReadOnly Property Wf As CompositeWorkflow
        Get
            Return _main.Workflow
        End Get
    End Property

    Private Function SegmentationInstalled() As Boolean
        Return _main.Models.Manifests.Any(Function(m) m.Purpose = "segmentation" AndAlso m.Installable AndAlso _main.Models.GetStatus(m.Id).IsInstalled)
    End Function

    Private Function BuildSteps() As List(Of TutorialStep)
        Dim nl = Environment.NewLine
        Dim useSample = Sub(file As String, load As Action(Of String))
                            Dim p = TutorialFile(file)
                            If Not IO.File.Exists(p) Then
                                MessageBox.Show(Me, $"找不到範例圖片：{p}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
                                Return
                            End If
                            load(p)
                        End Sub
        Return New List(Of TutorialStep) From {
            New TutorialStep With {
                .Title = "歡迎使用 AI Auto Composite Studio",
                .Image = "step5-blended.jpg", .ImageCaption = "這次教學要完成的成果：把賽博龐克女戰士合成到紫藤花隧道裡。",
                .Body = "這個教學會用兩張範例圖片，帶你走完一次完整的合成：" & nl & nl &
                        "1. 下載去背模型" & nl & "2. 開啟背景照片" & nl & "3. 開啟人物圖片" & nl & "4. 自動去背" & nl &
                        "5. 檢查與修正遮罩" & nl & "6. 自動定位" & nl & "7. 自動融合" & nl & "8. 手動微調" & nl & "9. 匯出與儲存" & nl & nl &
                        "每一步都有按鈕可以直接操作主視窗。完成後標題下方會出現 ✔，再按「下一步」。" & nl &
                        "不想用範例也可以：每一步都能改選自己的圖片。" & nl & nl &
                        "點示意圖可以放大。"},
            New TutorialStep With {
                .Title = "1. 下載去背模型",
                .Body = "程式不附 AI 模型（檔案大、各有授權），第一次使用要先下載。" & nl & nl &
                        "按下方「開啟模型管理」：" & nl &
                        "・選「BiRefNet ToonOut（動漫人物去背）」→「下載安裝」" & nl &
                        "・下載前會顯示檔案大小與授權，請閱讀後確認" & nl &
                        "・下載完會自動「測試載入」，看到「載入成功」就好了" & nl & nl &
                        "動漫、插畫人物用 ToonOut；真人照片可以再裝「BiRefNet（通用去背）」。" & nl &
                        "如果人物圖本來就是透明背景的 PNG，可以跳過這一步。",
                .Actions = New List(Of (String, Action)) From {("開啟模型管理", Sub() _main.ShowModelManager())},
                .IsDone = AddressOf SegmentationInstalled},
            New TutorialStep With {
                .Title = "2. 開啟背景照片",
                .Image = "sample-background.jpg", .ImageCaption = "範例背景：紫藤花隧道（1200×800）。",
                .Body = "背景是合成的舞台。程式會完整保留背景的原始解析度與像素，最後匯出時人物以外的地方都和原圖一模一樣。" & nl & nl &
                        "按「使用範例背景」，或工具列的「🖼 開啟背景」選自己的照片。" & nl & nl &
                        "載入後左側會顯示尺寸與格式。超大的掃描檔（超過 6400 萬像素）會自動等比縮小載入，資訊裡會註明原圖尺寸。",
                .Actions = New List(Of (String, Action)) From {
                    ("使用範例背景", Sub() useSample("sample-background.jpg", AddressOf _main.OpenBackgroundFile)),
                    ("選擇自己的照片…", Sub() _main.OpenBackgroundFile(PickFile("開啟背景照片")))},
                .IsDone = Function() Wf.Scene.Background IsNot Nothing},
            New TutorialStep With {
                .Title = "3. 開啟人物圖片",
                .Image = "step2-loaded.jpg", .ImageCaption = "還沒去背時，人物圖是整張不透明的長方形——這是正常的。",
                .Body = "按「使用範例人物」，或工具列的「🧍 開啟人物」。" & nl & nl &
                        "人物圖可以是：" & nl &
                        "・一般圖片（JPEG 等）：下一步用 AI 去背" & nl &
                        "・透明背景的 PNG：程式直接使用它的透明度，不用再去背" & nl & nl &
                        "範例人物是一般圖片，所以剛載入時會像示意圖這樣，整張連背景一起蓋在紫藤花上。",
                .Actions = New List(Of (String, Action)) From {
                    ("使用範例人物", Sub() useSample("sample-figure.jpg", AddressOf _main.OpenFigureFile)),
                    ("選擇自己的人物…", Sub() _main.OpenFigureFile(PickFile("開啟人物圖片")))},
                .IsDone = Function() Wf.Scene.Layers.Count > 0},
            New TutorialStep With {
                .Title = "4. 自動去背",
                .Image = "step3-cutout.jpg", .ImageCaption = "去背結果（棋盤格是透明區域）。頭髮、肩上的武器都保留下來了。",
                .Body = "按「執行自動去背」。程式會用右側「去背」分頁選的模型，把人物從原本的城市背景分離出來。" & nl & nl &
                        "・有獨立顯示卡時用 DirectML，1024×1536 的圖約 10～20 秒（第一次含載入模型）" & nl &
                        "・記憶體少於 16 GB 的電腦，建議在「去背」分頁把執行裝置改成 CPU" & nl & nl &
                        "完成後畫布上的人物就只剩下人了。",
                .Actions = New List(Of (String, Action)) From {
                    ("執行自動去背", Sub() _main.RunCutout()),
                    ("看「去背」分頁", Sub() _main.ShowRightTab("去背"))},
                .IsDone = Function() Wf.SelectedLayer IsNot Nothing AndAlso Not String.IsNullOrEmpty(Wf.SelectedLayer.MaskModelId)},
            New TutorialStep With {
                .Title = "5. 檢查與修正遮罩",
                .Image = "step3-cleaned.jpg", .ImageCaption = "修正後：左上角城市霓虹招牌的淡淡殘影清掉了，武器完整保留。",
                .Body = "AI 去背不一定一次完美。把工具列的「預覽」切到「去背人物」或「Alpha 遮罩」檢查邊緣：" & nl & nl &
                        "・背景殘影（例如範例左上角「NO FUTURE」霓虹招牌留下的淡紅色）：" & nl &
                        "　在「去背」分頁把「黑點」調高到 0.15 左右，按「套用精修」" & nl &
                        "　還有殘留就按「編輯遮罩…」，用「移除筆刷」塗掉" & nl &
                        "・頭髮、武器被切掉：用「保留筆刷」補回來" & nl &
                        "・邊緣太硬：「邊緣羽化」調高一點" & nl & nl &
                        "每一筆修改都可以按 Ctrl+Z 復原；「還原 AI 遮罩」可以回到剛去背的狀態。" & nl &
                        "遮罩沒問題的話，這一步可以直接跳過。",
                .Actions = New List(Of (String, Action)) From {
                    ("預覽：去背人物", Sub() _main.SetPreviewMode(1)),
                    ("預覽：Alpha 遮罩", Sub() _main.SetPreviewMode(2)),
                    ("編輯遮罩…", Sub() _main.ShowMaskEditor()),
                    ("回到最終合成", Sub() _main.SetPreviewMode(5))}},
            New TutorialStep With {
                .Title = "6. 自動定位",
                .Image = "step4-placed.jpg", .ImageCaption = "範例人物是半身像：裁切邊貼齊畫面下緣，高度約畫面 60%，放在中央。",
                .Body = "按「自動定位」。程式會分析人物和背景，推薦擺放位置與大小：" & nl & nl &
                        "・全身像：找可以站的地方（例如對著天空的柱頂、平台），腳踩在上面" & nl &
                        "・半身像（像範例這樣下緣被裁切）：裁切邊貼齊畫面下緣，像人像照一樣從畫面底部「長出來」" & nl & nl &
                        "右側「定位」分頁會列出幾個候選位置和評分（例如中央、左三分之一、右三分之一），點一下就能換。" & nl & nl &
                        "想指定位置：用「點選目標錨點」在背景上點一下；想要固定大小：填「目標高度」後再按一次自動定位。",
                .Actions = New List(Of (String, Action)) From {
                    ("自動定位", Sub() _main.RunAutoPlace()),
                    ("看候選位置", Sub() _main.ShowRightTab("定位"))},
                .IsDone = Function() _main.HasPlacementCandidates},
            New TutorialStep With {
                .Title = "7. 自動融合",
                .Image = "step5-blended.jpg", .ImageCaption = "環境匹配後：人物的色溫往紫藤花的冷紫色靠攏，看起來不再像貼紙。",
                .Body = "按「自動融合」。程式會取樣人物附近的背景，輕度調整人物的亮度、對比、飽和度與色溫，讓光線一致。" & nl & nl &
                        "・調整幅度有上限，動漫的線條和配色會保留，不會被改成照片質感" & nl &
                        "・全身像會自動產生腳下的接觸陰影；半身像沒有踩在地上，不畫陰影" & nl & nl &
                        "不滿意可以在「色彩」分頁改：" & nl &
                        "・「原始風格」完全不改變人物" & nl &
                        "・「自訂調色」自己拉每一個滑桿" & nl &
                        "・「自動強度」控制往背景靠攏的程度",
                .Actions = New List(Of (String, Action)) From {
                    ("自動融合", Sub() _main.RunAutoBlend()),
                    ("看「色彩」分頁", Sub() _main.ShowRightTab("色彩"))},
                .IsDone = Function() Wf.SelectedLayer IsNot Nothing AndAlso Wf.SelectedLayer.Color.Mode <> ColorMode.Original},
            New TutorialStep With {
                .Title = "8. 手動微調",
                .Image = "step4-candidate-right.jpg", .ImageCaption = "例如把人物移到右三分之一，讓出中央的紫藤花隧道。",
                .Body = "自動結果只是起點，畫布上可以直接調整：" & nl & nl &
                        "・拖曳人物：移動" & nl &
                        "・Shift＋滾輪：縮放人物；Ctrl＋滾輪：旋轉" & nl &
                        "・方向鍵：微調 1 像素（按住 Shift 一次 10 像素）" & nl &
                        "・滾輪：放大縮小畫面；中鍵或按住空白鍵拖曳：平移畫面" & nl & nl &
                        "其他分頁：" & nl &
                        "・「變換」：輸入精確數值，或套用仰角透視" & nl &
                        "・「陰影」：調整陰影方向與濃度" & nl &
                        "・「遮擋」：用筆刷畫出擋在人物前面的東西（例如垂下的花串）" & nl & nl &
                        "隨時可以按 Ctrl+Z 復原、Ctrl+Y 重做。",
                .Actions = New List(Of (String, Action)) From {
                    ("看「變換」分頁", Sub() _main.ShowRightTab("變換")),
                    ("看「遮擋」分頁", Sub() _main.ShowRightTab("遮擋"))}},
            New TutorialStep With {
                .Title = "9. 匯出與儲存",
                .Image = "step5-blended.jpg", .ImageCaption = "匯出一律用背景原始解析度，從原圖重新合成。",
                .Body = "・「匯出圖片…」：存成 PNG（無損）或 JPEG。會以背景原始解析度重新計算，不會放大預覽圖" & nl &
                        "・「儲存專案…」：存成 .acsp.json，記下所有參數與遮罩，下次可以接著修改" & nl & nl &
                        "完成了！之後換上自己的背景與人物，流程都一樣：" & nl &
                        "開啟背景 → 開啟人物 → 自動去背 → 自動定位 → 自動融合 → 微調 → 匯出。" & nl & nl &
                        "更完整的說明在程式資料夾旁的「使用手冊」（Docs\使用手冊.md）。",
                .Actions = New List(Of (String, Action)) From {
                    ("匯出圖片…", Sub() _main.ExportImage()),
                    ("儲存專案…", Sub() _main.SaveProject())},
                .IsDone = Function() Not String.IsNullOrEmpty(Wf.Scene.Export.LastExportPath)}}
    End Function

    Private Function PickFile(title As String) As String
        Using dlg As New OpenFileDialog With {.Title = title, .Filter = WicImageCodec.OpenFileFilter}
            Return If(dlg.ShowDialog(Me) = DialogResult.OK, dlg.FileName, Nothing)
        End Using
    End Function

    Private Sub ShowStep(index As Integer)
        _index = Math.Clamp(index, 0, _steps.Count - 1)
        Dim s = _steps(_index)
        _progress.Text = $"第 {_index + 1} / {_steps.Count} 步"
        _title.Text = s.Title
        _body.Text = s.Body
        _body.SelectionLength = 0
        Dim old = _picture.Image
        _picture.Image = Nothing
        old?.Dispose()
        _picture.Visible = Not String.IsNullOrEmpty(s.Image)
        _caption.Visible = _picture.Visible
        If _picture.Visible Then
            Dim p = TutorialFile(s.Image)
            If File.Exists(p) Then
                ' 讀成記憶體中的複本，不鎖住檔案。
                Using fs = File.OpenRead(p), img = Image.FromStream(fs)
                    _picture.Image = New Bitmap(img)
                End Using
            End If
            _caption.Text = s.ImageCaption
        End If
        _actions.Controls.Clear()
        For Each a In s.Actions
            Dim run = a.Run
            Dim b As New Button() With {.Text = a.Text, .AutoSize = True, .Height = 30, .Margin = New Padding(0, 0, 6, 6)}
            AddHandler b.Click, Sub()
                                    If _main.IsBusy Then
                                        MessageBox.Show(Me, "主視窗正在處理中，請稍候。", Text) : Return
                                    End If
                                    run()
                                End Sub
            _actions.Controls.Add(b)
        Next
        _actions.Visible = s.Actions.Count > 0
        _prev.Enabled = _index > 0
        _next.Text = If(_index = _steps.Count - 1, "完成", "下一步 ▶")
        UpdateStatus()
    End Sub

    Private Sub UpdateStatus()
        If _index >= _steps.Count Then Return
        Dim s = _steps(_index)
        If s.IsDone Is Nothing Then
            _status.Text = ""
            Return
        End If
        Dim done = False
        Try
            done = s.IsDone.Invoke()
        Catch ex As Exception When TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is NullReferenceException
        End Try
        _status.Text = If(done, "✔ 這一步完成了，按「下一步」繼續。", If(_main.IsBusy, "⏳ 處理中…", "○ 尚未完成（也可以直接按「下一步」跳過）"))
        _status.ForeColor = If(done, Color.ForestGreen, SystemColors.GrayText)
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        _timer.Stop()
        _picture.Image?.Dispose()
        MyBase.OnFormClosed(e)
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = Keys.Escape Then Close() : Return True
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Private Sub ShowLargeImage()
        If _picture.Image Is Nothing Then Return
        Using f As New Form() With {.Text = _caption.Text, .Width = 1100, .Height = 800, .StartPosition = FormStartPosition.CenterParent, .KeyPreview = True}
            Dim pb As New PictureBox() With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom, .Image = _picture.Image, .BackColor = Color.FromArgb(30, 30, 30)}
            AddHandler pb.Click, Sub() f.Close()
            AddHandler f.KeyDown, Sub() f.Close()
            f.Controls.Add(pb)
            f.ShowDialog(Me)
            pb.Image = Nothing
        End Using
    End Sub
End Class

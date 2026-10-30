Imports System.IO
Imports AutoComposite

''' <summary>
''' 使用手冊教學圖產生器：照主程式的步驟跑一遍合成流程，每一步輸出一張 PNG。
''' 參數：背景 PNG、人物 PNG、去背模型（.onnx）、輸出資料夾。
''' 模型以 rembg 的 IS-Net anime 規格處理：輸入 1024 拉伸成正方形（這個模型遇到補邊的輸入會整張判成背景）、
''' 平均 (0.485, 0.456, 0.406)、標準差 1、輸出最小最大值正規化。
''' </summary>
Module Program
    Function Main(args As String()) As Integer
        If args.Length < 4 Then
            Console.Error.WriteLine("用法：TutorialImages <背景.png> <人物.png> <模型.onnx> <輸出資料夾>")
            Return 2
        End If
        Dim bgPath = args(0), figPath = args(1), modelPath = args(2), outDir = args(3)
        Directory.CreateDirectory(outDir)

        Dim svc = CompositeServices.Create(Nothing, Nothing)
        Dim manifest As New ModelManifest With {
            .Id = "isnet-anime", .Name = "IS-Net anime", .Purpose = "segmentation",
            .Io = New ManifestIo With {.InputShape = {1, 3, 1024, 1024}, .InputType = "float32"},
            .Preprocess = New ManifestPreprocess With {.Size = 1024, .Mean = {0.485, 0.456, 0.406}, .Std = {1.0, 1.0, 1.0},
                                                       .Resize = "stretch", .OutputActivation = "auto", .MinMaxNormalize = True}}
        svc.BackgroundRemovers(manifest.Id) = New OnnxBackgroundRemovalService(manifest, Function() modelPath)
        Dim wf As New CompositeWorkflow(svc)
        Dim save = Sub(name As String, img As RgbaImage)
                       Dim p = Path.Combine(outDir, name)
                       Call New PngCodec().Encode(img, p, ExportFormat.Png, 0)
                       Console.WriteLine($"{name}  {img.Width}×{img.Height}")
                   End Sub
        Dim render = Function(mode As PreviewMode) svc.Renderer.Render(wf.Scene, New RenderOptions With {.Mode = mode, .MaxSide = 1200, .LayerId = wf.SelectedLayerId}).Image

        ' 步驟 1～2：載入背景與人物。
        Dim bgInfo = wf.LoadBackground(bgPath)
        Console.WriteLine("背景：" & bgInfo.Describe())
        Dim layer = wf.AddFigure(figPath)
        Console.WriteLine("人物：" & layer.SourceInfo.Describe())
        save("step2-loaded.png", render(PreviewMode.Final))

        ' 步驟 3：自動去背＋精修。
        Dim seg = wf.AutoCutoutAsync(layer, manifest.Id, New SegmentationOptions With {.Preference = ExecutionPreference.Cpu}, New MaskRefineOptions(), Nothing, Nothing).Result
        Console.WriteLine($"去背：{seg.Provider}，{seg.Milliseconds:0} ms，啟動 {seg.Activation}")
        save("step3-alpha.png", render(PreviewMode.AlphaMask))
        save("step3-cutout.png", render(PreviewMode.CutoutFigure))

        ' 步驟 3b：清掉左上角霓虹招牌的殘影——黑點調高，再用移除筆刷塗掉身體外的淡淡殘留。
        wf.ReRefine(layer, New MaskRefineOptions With {.BlackPoint = 0.15})
        Dim w = layer.Source.Width, h = layer.Source.Height
        wf.PaintFigureMask(layer, New BrushStroke With {.Mode = BrushMode.Remove, .Radius = w * 0.07, .Softness = 0.5,
            .Points = New List(Of PointD) From {New PointD(w * 0.04, h * 0.27), New PointD(w * 0.1, h * 0.31), New PointD(w * 0.05, h * 0.35)}})
        save("step3-cleaned.png", render(PreviewMode.CutoutFigure))

        ' 步驟 4：自動定位（半身像：貼齊畫面下緣）。
        Dim cands = wf.SuggestPlacements(layer)
        wf.ApplyPlacement(layer, cands(0))
        Console.WriteLine("定位：" & cands(0).Description)
        save("step4-placed.png", render(PreviewMode.Final))
        ' 另一個候選：右三分之一（給「點選候選位置」那一步看）。
        Dim right = cands.FirstOrDefault(Function(x) x.Description.Contains("右三分之一"))
        If right IsNot Nothing Then
            Dim keep = layer.Transform.Clone()
            layer.Transform = right.Transform.Clone()
            save("step4-candidate-right.png", render(PreviewMode.Final))
            layer.Transform = keep
        End If

        ' 步驟 5：自動融合（環境匹配）。
        wf.AutoBlend(layer, New AutoBlendOptions With {.ColorMode = ColorMode.EnvironmentMatch, .Strength = 0.6})
        Dim c = layer.Color
        Console.WriteLine($"融合：亮度 {c.Brightness:0.000}，對比 {c.Contrast:0.000}，飽和 {c.Saturation:0.000}，色溫 {c.Temperature:0.000}，色調 {c.Tint:0.000}，陰影 {layer.Shadow.Enabled}")
        save("step5-blended.png", render(PreviewMode.Final))

        ' 步驟 6：匯出（背景原尺寸）。
        Dim rep = wf.ExportImage(Path.Combine(outDir, "step6-result.png"), ExportFormat.Png, 95, Nothing)
        Console.WriteLine($"匯出：{rep.Width}×{rep.Height}，{rep.Milliseconds:0} ms")
        svc.UnloadModels()
        Return 0
    End Function
End Module

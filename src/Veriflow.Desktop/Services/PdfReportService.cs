using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Veriflow.Desktop.Models;
using Veriflow.Core.Models;

namespace Veriflow.Desktop.Services
{
    public class PdfReportService
    {
        public PdfReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public void GeneratePdf(string filePath, ReportHeader header, IEnumerable<ReportItem> items, bool isVideo, ReportSettings settings)
        {
            // Calculate Total Size
            long totalBytes = 0;
            foreach (var item in items)
            {
                try
                {
                   if (File.Exists(item.OriginalMedia?.FullName))
                   {
                       totalBytes += new FileInfo(item.OriginalMedia.FullName).Length;
                   }
                }
                catch { /* Ignore access errors */ }
            }
            string totalSizeStr = FormatBytes(totalBytes);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    // Portrait is usually better for lists unless many columns.
                    // 7 Columns -> Landscape might be safer, but let's stick to Portrait default or user choice.
                    // Given the columns (Filename 3, Scene 1, Take 1, TC 80, Dur 80, Notes 3), it fits A4 Portrait tightly.
                    // Let's go Landscape to be safe and "Pro".
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                    page.Header().Element(compose => ComposeHeader(compose, header, isVideo, totalSizeStr, settings));
                    page.Content().Element(compose => ComposeContent(compose, items, isVideo, settings));
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(filePath);
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n2} {1}", number, suffixes[counter]);
        }

        private void ComposeHeader(IContainer container, ReportHeader header, bool isVideo, string totalSize, ReportSettings settings)
        {
            // Title Logic
            string title = isVideo ? "CAMERA REPORT" : "SOUND REPORT";
            string brandingColor = isVideo ? "#1A4CB1" : "#D32F2F"; // Blue or Red accent

            // Logo Path
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "veriflow.ico");
            if (!File.Exists(logoPath))
            {
                 // Fallback to strict source path since we are in dev environment
                 logoPath = @"d:\ELEMENT\VERIFLOW\src\Veriflow.Desktop\Assets\veriflow.ico";
            }

            // Custom Title
            if (settings.UseCustomTitle && !string.IsNullOrWhiteSpace(settings.CustomTitle))
            {
                title = settings.CustomTitle;
            }

            // Custom Logo
            if (settings.UseCustomLogo && File.Exists(settings.CustomLogoPath))
            {
                logoPath = settings.CustomLogoPath;
            }

            container.Column(column =>
            {
                // Banner / Title
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(title).FontSize(24).ExtraBold().FontColor(brandingColor);
                    
                    // Logo Integration
                    if (File.Exists(logoPath))
                    {
                        // Use AutoItem to allow width to adjust based on aspect ratio
                        // constrain height explicitly to 40 units
                        row.AutoItem().Height(40).AlignRight().Image(logoPath).FitHeight();
                    }
                    else
                    {
                         row.RelativeItem().AlignRight().Text("VERIFLOW").FontSize(14).SemiBold().FontColor(Colors.Grey.Medium);
                    }
                });

                column.Item().PaddingTop(10).LineHorizontal(2).LineColor(brandingColor);

                // Metadata Grid
                column.Item().PaddingTop(10).Row(row =>
                {
                    // LEFT COLUMN
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(t => { t.Span("Project: ").SemiBold(); t.Span(header.ProjectName); });
                        c.Item().Text(t => { t.Span("Date: ").SemiBold(); t.Span(header.ReportDate); });
                        
                        if (isVideo)
                        {
                             c.Item().Text(t => { t.Span("Director: ").SemiBold(); t.Span(header.Director); });
                             c.Item().Text(t => { t.Span("DOP: ").SemiBold(); t.Span(header.Dop); });
                             c.Item().Text(t => { t.Span("Operator: ").SemiBold(); t.Span(header.OperatorName); });
                        }
                        else
                        {
                             c.Item().Text(t => { t.Span("Location: ").SemiBold(); t.Span(header.Location); });
                             c.Item().Text(t => { t.Span("Sound Mixer: ").SemiBold(); t.Span(header.SoundMixer); });
                             c.Item().Text(t => { t.Span("Boom Op: ").SemiBold(); t.Span(header.BoomOperator); });
                        }
                    });

                    // RIGHT COLUMN
                    row.RelativeItem().Column(c =>
                    {
                        if (isVideo)
                        {
                            c.Item().Text(t => { t.Span("Data Manager: ").SemiBold(); t.Span(header.DataManager); });
                            c.Item().Text(t => { t.Span("Cam ID: ").SemiBold(); t.Span(header.CameraId); });
                            c.Item().Text(t => { t.Span("Roll: ").SemiBold(); t.Span(header.ReelName); });
                        }
                        else
                        {
                            c.Item().Text(t => { t.Span("Timecode Rate: ").SemiBold(); t.Span(header.TimecodeRate); });
                            c.Item().Text(t => { t.Span("Files Info: ").SemiBold(); t.Span(header.FilesType); });
                        }
                        
                         c.Item().Text(t => { t.Span("Total Size: ").SemiBold(); t.Span(totalSize); });
                    });
                });
                
                column.Item().PaddingTop(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
        }

        private void ComposeContent(IContainer container, IEnumerable<ReportItem> items, bool isVideo, ReportSettings settings)
        {
            var itemList = items.ToList();

            if (isVideo)
            {
                ComposeVideoTable(container, itemList, settings);
            }
            else
            {
                ComposeAudioTable(container, itemList, settings);
            }
        }

        private void ComposeVideoTable(IContainer container, List<ReportItem> itemList, ReportSettings settings)
        {
             container.PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    if (settings.ShowFilename) columns.RelativeColumn(1.5f);
                    if (settings.ShowScene) columns.RelativeColumn(0.8f);
                    if (settings.ShowTake) columns.RelativeColumn(0.6f);
                    if (settings.ShowTimecode) columns.ConstantColumn(85);
                    if (settings.ShowDuration) columns.ConstantColumn(85);
                    if (settings.ShowFps) columns.ConstantColumn(35);
                    if (settings.ShowIso || settings.ShowWhiteBalance) columns.ConstantColumn(70);
                    if (settings.ShowCodecResultion) columns.RelativeColumn(1.5f);
                    if (settings.ShowNotes) columns.RelativeColumn(2f);
                });

                // Header
                table.Header(header =>
                {
                    if (settings.ShowFilename) header.Cell().Element(CellStyle).Text("Filename");
                    if (settings.ShowScene) header.Cell().Element(CellStyle).Text("Scene");
                    if (settings.ShowTake) header.Cell().Element(CellStyle).Text("Take");
                    if (settings.ShowTimecode) header.Cell().Element(CellStyle).Text("Start TC");
                    if (settings.ShowDuration) header.Cell().Element(CellStyle).Text("Duration");
                    if (settings.ShowFps) header.Cell().Element(CellStyle).Text("FPS");
                    if (settings.ShowIso || settings.ShowWhiteBalance) header.Cell().Element(CellStyle).Text("ISO / WB");
                    if (settings.ShowCodecResultion) header.Cell().Element(CellStyle).Text("Codec / Res");
                    if (settings.ShowNotes) header.Cell().Element(CellStyle).Text("Notes");
                });

                // Content
                for (int i = 0; i < itemList.Count; i++)
                {
                    var item = itemList[i];
                    var bgColor = i % 2 == 0 ? "#F5F5F5" : "#FFFFFF";
                    
                    if (settings.ShowFilename) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(item.Filename).FontSize(8);
                    if (settings.ShowScene) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(item.Scene ?? "");
                    if (settings.ShowTake) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(item.Take ?? "");
                    if (settings.ShowTimecode) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(item.StartTimeCode ?? "").FontFamily(Fonts.CourierNew);
                    if (settings.ShowDuration) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(item.Duration ?? "").FontFamily(Fonts.CourierNew);
                    if (settings.ShowFps) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(item.Fps ?? "");
                    
                    if (settings.ShowIso || settings.ShowWhiteBalance)
                    {
                        // ISO / WB Logic
                        string iso = string.IsNullOrWhiteSpace(item.Iso) || item.Iso == "N/A" ? "" : item.Iso;
                        string wb = string.IsNullOrWhiteSpace(item.WhiteBalance) || item.WhiteBalance == "N/A" ? "" : item.WhiteBalance;
                        string isoWbDisplay = "";
                        // If one is hidden? Logic assumes combined column.
                        // Ideally we respect individual flags if possible, but for table layout simplicity we group or check if ANY.
                        // Here simple logic: Show whatever is available if column is shown.
                        bool showIso = settings.ShowIso && !string.IsNullOrEmpty(iso);
                        bool showWb = settings.ShowWhiteBalance && !string.IsNullOrEmpty(wb);
                         
                        if (showIso && showWb) isoWbDisplay = $"{iso} / {wb}";
                        else if (showIso) isoWbDisplay = iso;
                        else if (showWb) isoWbDisplay = wb;
                        
                        table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(isoWbDisplay);
                    }
                    
                    if (settings.ShowCodecResultion)
                    {
                        table.Cell().Element(c => BodyCellStyle(c, bgColor)).Column(col => 
                        {
                            col.Item().Text(item.Codec ?? "").FontSize(8);
                            col.Item().Text(item.Resolution ?? "").FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                    }

                    if (settings.ShowNotes) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(item.ItemNotes ?? "");
                }
            });
        }

        private void ComposeAudioTable(IContainer container, List<ReportItem> itemList, ReportSettings settings)
        {
            // Interleaved Paging Logic:
            // A1, A2, B1, B2...
            // Chunk items to fit on one page (approx 18 rows).
            // For each chunk, allow Part 1 to render (Page 1), then PageBreak, then Part 2 (Page 2).
            
            int itemsPerPage = 18; // Estimated safe limit for A4 Landscape
            var chunks = itemList.Chunk(itemsPerPage).ToList();

            container.Column(column => 
            {
                foreach (var chunkArr in chunks)
                {
                    var chunk = chunkArr.ToList();
                    
                    // Part 1: Basic Info
                    column.Item().PaddingTop(10).Element(c => ComposeAudioTablePart1(c, chunk, settings));
                    
                    // Force Page Break to show Tracks for THIS chunk on the NEXT page
                    column.Item().PageBreak();

                    // Part 2: Tracks Info
                    // Check if Tracks are enabled
                    if (settings.ShowTracks)
                    {
                         column.Item().Element(c => ComposeAudioTablePart2(c, chunk, settings));
                    }

                    // If not the last chunk, add another Page Break to start the next chunk cleanly
                    if (chunkArr != chunks.Last())
                    {
                        column.Item().PageBreak();
                    }
                }
            });
        }

        private void ComposeAudioTablePart1(IContainer container, List<ReportItem> itemList, ReportSettings settings)
        {
             container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    if (settings.ShowFilename) columns.RelativeColumn(1.5f);
                    if (settings.ShowScene) columns.RelativeColumn(1);
                    if (settings.ShowTake) columns.RelativeColumn(1);
                    if (settings.ShowTimecode) columns.ConstantColumn(85);
                    if (settings.ShowDuration) columns.ConstantColumn(85);
                    if (settings.ShowSampleRate) columns.RelativeColumn(0.8f);
                    if (settings.ShowBitDepth) columns.RelativeColumn(0.6f);
                    if (settings.ShowNotes) columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    if (settings.ShowFilename) header.Cell().Element(CellStyle).Text("Filename");
                    if (settings.ShowScene) header.Cell().Element(CellStyle).Text("Scene");
                    if (settings.ShowTake) header.Cell().Element(CellStyle).Text("Take");
                    if (settings.ShowTimecode) header.Cell().Element(CellStyle).Text("Start TC");
                    if (settings.ShowDuration) header.Cell().Element(CellStyle).Text("Duration");
                    if (settings.ShowSampleRate) header.Cell().Element(CellStyle).Text("SR");
                    if (settings.ShowBitDepth) header.Cell().Element(CellStyle).Text("Bit");
                    if (settings.ShowNotes) header.Cell().Element(CellStyle).Text("Notes");
                });

                for (int i = 0; i < itemList.Count; i++)
                {
                    var item = itemList[i];
                    var bgColor = i % 2 == 0 ? "#F5F5F5" : "#FFFFFF";
                    bool isCircled = item.IsCircled;

                    if (settings.ShowFilename) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.Filename).FontSize(9); if (isCircled) s.SemiBold(); });
                    if (settings.ShowScene) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.Scene ?? ""); if (isCircled) s.SemiBold(); });
                    if (settings.ShowTake) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.Take ?? ""); if (isCircled) s.SemiBold(); });
                    if (settings.ShowTimecode) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.StartTimeCode ?? "").FontFamily(Fonts.CourierNew); if (isCircled) s.SemiBold(); });
                    if (settings.ShowDuration) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.Duration ?? "").FontFamily(Fonts.CourierNew); if (isCircled) s.SemiBold(); });
                    if (settings.ShowSampleRate) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.SampleRate ?? ""); if (isCircled) s.SemiBold(); });
                    if (settings.ShowBitDepth) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.BitDepth ?? ""); if (isCircled) s.SemiBold(); });
                    if (settings.ShowNotes) table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.ItemNotes ?? ""); if (isCircled) s.SemiBold(); });
                }
            });
        }

        private void ComposeAudioTablePart2(IContainer container, List<ReportItem> itemList, ReportSettings settings)
        {
            // Calculate Max Tracks
            int maxTracks = 0;
            foreach (var item in itemList)
            {
                if (!string.IsNullOrEmpty(item.Tracks))
                {
                    int count = item.Tracks.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                     if (count > maxTracks) maxTracks = count;
                }
            }

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.5f); // Filename (Repetition reference)
                    // Dynamic Tracks
                    for (int t = 0; t < maxTracks; t++)
                    {
                        columns.RelativeColumn(1f);
                    }
                    columns.ConstantColumn(40);   // Circle
                    columns.RelativeColumn(2f);   // Notes
                });

                table.Header(header =>
                {
                   header.Cell().Element(CellStyle).Text("Filename");
                    for (int t = 0; t < maxTracks; t++)
                    {
                        header.Cell().Element(CellStyle).Text($"T{t + 1}");
                    }
                    header.Cell().Element(CellStyle).Text("Cirl");
                    header.Cell().Element(CellStyle).Text("Notes");
                });

                for (int i = 0; i < itemList.Count; i++)
                {
                    var item = itemList[i];
                    var bgColor = i % 2 == 0 ? "#F5F5F5" : "#FFFFFF";
                    bool isCircled = item.IsCircled;

                    table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.Filename).FontSize(9); if (isCircled) s.SemiBold(); });

                    // Tracks
                    var trackNames = new List<string>();
                    if (!string.IsNullOrEmpty(item.Tracks))
                    {
                        var parts = item.Tracks.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in parts)
                        {
                            var split = part.Split(':');
                            if (split.Length > 1) trackNames.Add(split[1]);
                            else trackNames.Add(part);
                        }
                    }

                    for (int t = 0; t < maxTracks; t++)
                    {
                        string tName = t < trackNames.Count ? trackNames[t] : "";
                        table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(txt => 
                        {
                            var s = txt.Span(tName).FontSize(7); // Smaller font for tracks
                            if (isCircled) s.SemiBold();
                        });
                    }
                    
                    // Circle
                     table.Cell().Element(c => BodyCellStyle(c, bgColor)).AlignMiddle().AlignCenter().Text(t => 
                    {
                        if (item.IsCircled) t.Span("X").Bold().FontColor(Colors.Red.Medium);
                    });

                    // Notes
                    table.Cell().Element(c => BodyCellStyle(c, bgColor)).Text(t => { var s = t.Span(item.ItemNotes ?? ""); if (isCircled) s.SemiBold(); });
                }
            });
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container.Background("#333333").Padding(6).DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White).FontSize(9));
        }

        private static IContainer BodyCellStyle(IContainer container, string backgroundColor)
        {
            return container.Background(backgroundColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignMiddle(); // Reduced padding
        }

        private void ComposeFooter(IContainer container)
        {
            container.PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });

                row.RelativeItem().AlignRight().Text("Generated by Veriflow 1.7.0 (Beta)");
            });
        }
    }
}

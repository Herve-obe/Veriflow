using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// Service to generate user documentation PDF
    /// </summary>
    public static class DocumentationService
    {
        public static void GenerateUserGuidePDF(string outputPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Text("Veriflow Pro - User Guide")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Spacing(20);

                            // Title Page
                            x.Item().AlignCenter().Text("Veriflow Pro").FontSize(32).Bold();
                            x.Item().AlignCenter().Text("User Guide").FontSize(24).SemiBold();
                            x.Item().AlignCenter().Text("Version 1.0.0").FontSize(14);
                            x.Item().AlignCenter().PaddingTop(10).Text("© 2025 Veriflow. All rights reserved.").FontSize(10);

                            x.Item().PageBreak();

                            // Table of Contents
                            x.Item().Text("Table of Contents").FontSize(18).Bold();
                            x.Item().PaddingLeft(20).Column(toc =>
                            {
                                toc.Item().Text("1. Introduction");
                                toc.Item().Text("2. Installation");
                                toc.Item().Text("3. Getting Started");
                                toc.Item().Text("4. Features Overview");
                                toc.Item().Text("5. Keyboard Shortcuts");
                                toc.Item().Text("6. Session Management");
                                toc.Item().Text("7. Troubleshooting");
                                toc.Item().Text("8. Legal Information");
                            });

                            x.Item().PageBreak();

                            // Introduction
                            x.Item().Text("1. Introduction").FontSize(16).Bold();
                            x.Item().Text("Veriflow is a professional media verification and workflow tool designed for video and audio production environments. It provides comprehensive tools for media playback, quality control, transcoding, and secure file operations.");

                            x.Item().PageBreak();

                            // Installation
                            x.Item().Text("2. Installation").FontSize(16).Bold();
                            x.Item().Text("System Requirements:").SemiBold();
                            x.Item().PaddingLeft(20).Column(req =>
                            {
                                req.Item().Text("• Windows 10/11 (64-bit)");
                                req.Item().Text("• .NET 8.0 Runtime");
                                req.Item().Text("• 8GB RAM minimum (16GB recommended)");
                                req.Item().Text("• 500MB free disk space");
                            });

                            x.Item().PageBreak();

                            // Getting Started
                            x.Item().Text("3. Getting Started").FontSize(16).Bold();
                            x.Item().Text("Upon first launch, Veriflow opens to the SECURE COPY page. Navigate between modules using:");
                            x.Item().PaddingLeft(20).Column(nav =>
                            {
                                nav.Item().Text("• Function keys (F1-F6)");
                                nav.Item().Text("• Menu: View > [Module Name]");
                                nav.Item().Text("• Bottom navigation bar");
                            });

                            x.Item().PageBreak();

                            // Features Overview
                            x.Item().Text("4. Features Overview").FontSize(16).Bold();
                            x.Item().Column(features =>
                            {
                                features.Item().Text("SECURE COPY (F1)").SemiBold();
                                features.Item().PaddingLeft(20).Text("Dual-destination file copying with hash verification.");

                                features.Item().PaddingTop(10).Text("MEDIA (F2)").SemiBold();
                                features.Item().PaddingLeft(20).Text("File browser and media library management.");

                                features.Item().PaddingTop(10).Text("PLAYER (F3)").SemiBold();
                                features.Item().PaddingLeft(20).Text("Professional audio/video playback with metadata display.");

                                features.Item().PaddingTop(10).Text("SYNC (F4)").SemiBold();
                                features.Item().PaddingLeft(20).Text("Synchronization tools for multi-camera workflows.");

                                features.Item().PaddingTop(10).Text("TRANSCODE (F5)").SemiBold();
                                features.Item().PaddingLeft(20).Text("Format conversion and encoding tools.");

                                features.Item().PaddingTop(10).Text("REPORTS (F6)").SemiBold();
                                features.Item().PaddingLeft(20).Text("Quality control reporting and EDL generation.");
                            });

                            x.Item().PageBreak();

                            // Keyboard Shortcuts
                            x.Item().Text("5. Keyboard Shortcuts").FontSize(16).Bold();
                            x.Item().Column(shortcuts =>
                            {
                                shortcuts.Item().Text("Global Shortcuts:").SemiBold();
                                shortcuts.Item().PaddingLeft(20).Column(global =>
                                {
                                    global.Item().Text("Ctrl+N - New Session");
                                    global.Item().Text("Ctrl+O - Open Session");
                                    global.Item().Text("Ctrl+S - Save Session");
                                    global.Item().Text("Ctrl+C - Copy");
                                    global.Item().Text("Ctrl+V - Paste");
                                    global.Item().Text("Ctrl+Tab - Toggle Audio/Video mode");
                                    global.Item().Text("F1 - Help");
                                });

                                shortcuts.Item().PaddingTop(10).Text("Navigation:").SemiBold();
                                shortcuts.Item().PaddingLeft(20).Column(nav =>
                                {
                                    nav.Item().Text("F1 - SECURE COPY");
                                    nav.Item().Text("F2 - MEDIA");
                                    nav.Item().Text("F3 - PLAYER");
                                    nav.Item().Text("F4 - SYNC");
                                    nav.Item().Text("F5 - TRANSCODE");
                                    nav.Item().Text("F6 - REPORTS");
                                });

                                shortcuts.Item().PaddingTop(10).Text("Player Controls:").SemiBold();
                                shortcuts.Item().PaddingLeft(20).Column(player =>
                                {
                                    player.Item().Text("Space - Play/Pause");
                                    player.Item().Text("Enter - Stop");
                                    player.Item().Text("Left/Right - Frame step");
                                });
                            });

                            x.Item().PageBreak();

                            // Session Management
                            x.Item().Text("6. Session Management").FontSize(16).Bold();
                            x.Item().Text("A session saves your workspace state including loaded media, reports, and transcode queue.");
                            x.Item().PaddingTop(10).Text("Session Operations:").SemiBold();
                            x.Item().PaddingLeft(20).Column(ops =>
                            {
                                ops.Item().Text("• New Session (Ctrl+N) - Clears workspace");
                                ops.Item().Text("• Open Session (Ctrl+O) - Loads .vfsession file");
                                ops.Item().Text("• Save Session (Ctrl+S) - Saves current state");
                            });

                            x.Item().PageBreak();

                            // Troubleshooting
                            x.Item().Text("7. Troubleshooting").FontSize(16).Bold();
                            x.Item().Column(trouble =>
                            {
                                trouble.Item().Text("Application won't start:").SemiBold();
                                trouble.Item().PaddingLeft(20).Text("• Verify .NET 8.0 Runtime is installed");
                                trouble.Item().PaddingLeft(20).Text("• Check crash log: Veriflow_CrashLog.txt on Desktop");

                                trouble.Item().PaddingTop(10).Text("Media won't play:").SemiBold();
                                trouble.Item().PaddingLeft(20).Text("• Verify file format is supported");
                                trouble.Item().PaddingLeft(20).Text("• Check codec compatibility");

                                trouble.Item().PaddingTop(10).Text("Log Files:").SemiBold();
                                trouble.Item().PaddingLeft(20).Text("Access via: Help > Open Log Folder");
                            });

                            x.Item().PageBreak();

                            // Legal Information
                            x.Item().Text("8. Legal Information").FontSize(16).Bold();
                            x.Item().Text("Veriflow uses the following third-party libraries:");
                            x.Item().PaddingLeft(20).Column(legal =>
                            {
                                legal.Item().Text("• FFmpeg (LGPL v2.1 / GPL v2+) - https://ffmpeg.org");
                                legal.Item().Text("• LibVLC (LGPL v2.1+) - https://www.videolan.org");
                                legal.Item().Text("• .NET 8.0 Runtime (MIT)");
                                legal.Item().Text("• CSCore (MS-PL)");
                                legal.Item().Text("• QuestPDF (MIT)");
                                legal.Item().Text("• MathNet.Numerics (MIT)");
                            });
                            x.Item().PaddingTop(10).Text("For full license information, see Help > About Veriflow.");
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            })
            .GeneratePdf(outputPath);
        }
    }
}

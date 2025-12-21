using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Markup;
using Veriflow.Desktop.Models;

namespace Veriflow.Desktop.Services
{
    public class ReportPrintingService : IReportPrintingService
    {
        public void PrintReport(ReportHeader header, IEnumerable<ReportItem> items, ReportType type)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // LANDSCAPE LOGIC
                    // We need to set the PrintTicket to Landscape if possible, or adjust dimensions.
                    try 
                    {
                        printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
                    } 
                    catch { /* Driver might not support setting this programmatically */ }

                    FlowDocument doc = CreateFlowDocument(header, items, type);
                    
                    // Force Landscape dimensions
                    double pageWidth = 1122; // A4 Landscape approx (or just swap dimensions)
                    double pageHeight = 793;
                    
                    if (printDialog.PrintableAreaWidth > printDialog.PrintableAreaHeight)
                    {
                         // Driver is in landscape
                         pageWidth = printDialog.PrintableAreaWidth;
                         pageHeight = printDialog.PrintableAreaHeight;
                    }
                    else
                    {
                        // Driver is portrait, we rotate logic or request rotation
                        // For FlowDocument, we just set the PageWidth/Height. 
                        // The printer will clip if not set to Landscape in dialog.
                        // Assuming user selects Landscape in dialog or we guided them.
                        // Ideally we swap:
                         pageWidth = printDialog.PrintableAreaHeight;
                         pageHeight = printDialog.PrintableAreaWidth;
                    }

                    doc.PageHeight = pageHeight;
                    doc.PageWidth = pageWidth;
                    doc.PagePadding = new Thickness(40);
                    doc.ColumnGap = 0;
                    doc.ColumnWidth = pageWidth;

                    IDocumentPaginatorSource idpSource = doc;
                    printDialog.PrintDocument(idpSource.DocumentPaginator, $"{header.ProjectName} - Report");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing report: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument CreateFlowDocument(ReportHeader header, IEnumerable<ReportItem> items, ReportType type)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10, // Slightly smaller for high density
                Background = Brushes.White,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Left
            };

            // --- HEADER ---
            // Dark Grey Header Container
            var headerContainer = new Section
            {
                Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)), // #333333
                Foreground = Brushes.White,
                Padding = new Thickness(20)
            };

            // Title
            string titleType = type == ReportType.Audio ? "SOUND REPORT" : "CAMERA REPORT";
            var titlePara = new Paragraph(new Run($"VERIFLOW PRO | {titleType}"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            headerContainer.Blocks.Add(titlePara);

            // Info Table
            var headerTable = new Table { CellSpacing = 5, Background = Brushes.Transparent };
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var headerRowGroup = new TableRowGroup();
            
            // Row 1
            var r1 = new TableRow();
            r1.Cells.Add(CreateHeaderInfoCell("PROJECT", header.ProjectName));
            r1.Cells.Add(CreateHeaderInfoCell("DATE", header.ReportDate));
            r1.Cells.Add(CreateHeaderInfoCell("EPISODE / SCENE", $"{header.Episode} / {header.Scene}"));
            headerRowGroup.Rows.Add(r1);

            // Row 2
            var r2 = new TableRow();
            r2.Cells.Add(CreateHeaderInfoCell("PRODUCTION CO", header.ProductionCompany));
            
            if (type == ReportType.Audio)
            {
                 r2.Cells.Add(CreateHeaderInfoCell("SOUND MIXER", header.SoundMixer));
            }
            else
            {
                 r2.Cells.Add(CreateHeaderInfoCell("DIRECTOR", header.Director));
                 r2.Cells.Add(CreateHeaderInfoCell("DOP", header.Dop));
            }
            r2.Cells.Add(CreateHeaderInfoCell("OPERATOR", header.OperatorName)); // Overload into last col or add new row
            
            headerRowGroup.Rows.Add(r2);
            headerTable.RowGroups.Add(headerRowGroup);
            headerContainer.Blocks.Add(headerTable);
            
            doc.Blocks.Add(headerContainer);

            // GLOBAL NOTES
             if (!string.IsNullOrWhiteSpace(header.GlobalNotes))
            {
                doc.Blocks.Add(new Paragraph(new Run("NOTES:")) { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 0) });
                var notesPara = new Paragraph(new Run(header.GlobalNotes)) 
                { 
                    FontStyle = FontStyles.Italic, 
                    Padding = new Thickness(5),
                    Background = Brushes.WhiteSmoke
                };
                doc.Blocks.Add(notesPara);
            }

            // --- ITEM TABLE ---
            var itemsTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 20, 0, 0), BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 0) };
            
            // Define Columns based on Type
            if (type == ReportType.Audio)
            {
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(30) });  // OK (Circle)
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(50) });  // Scene
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(40) });  // Take
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(80) });  // TC
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(60) });  // Dur
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(150) }); // Filename
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Tracks
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(50) });  // SR
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Notes
            }
            else // Video
            {
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(30) });  // OK
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(80) });  // Thumb
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(50) });  // Scene
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(40) });  // Take
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(80) });  // TC
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(60) });  // Dur
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(150) }); // Filename
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(60) });  // Res
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(50) });  // Codec
                itemsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Notes
            }

            var itemsGroup = new TableRowGroup();

            // Header Row
            var tableHeader = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            tableHeader.Cells.Add(CreateTextCell("OK"));
            if (type == ReportType.Video) tableHeader.Cells.Add(CreateTextCell("IMG"));
            tableHeader.Cells.Add(CreateTextCell("SCENE"));
            tableHeader.Cells.Add(CreateTextCell("TK"));
            tableHeader.Cells.Add(CreateTextCell("TC START"));
            tableHeader.Cells.Add(CreateTextCell("DUR"));
            tableHeader.Cells.Add(CreateTextCell("FILENAME"));
            
            if (type == ReportType.Audio)
            {
                tableHeader.Cells.Add(CreateTextCell("TRACKS"));
                tableHeader.Cells.Add(CreateTextCell("SR"));
            }
            else
            {
                tableHeader.Cells.Add(CreateTextCell("RES"));
                tableHeader.Cells.Add(CreateTextCell("CODEC"));
            }
            tableHeader.Cells.Add(CreateTextCell("NOTES"));
            itemsGroup.Rows.Add(tableHeader);

            // Data Rows (Zebra)
            int index = 0;
            foreach (var item in items)
            {
                var row = new TableRow();
                row.Background = (index % 2 == 0) ? Brushes.White : new SolidColorBrush(Color.FromRgb(240, 240, 240)); // Zebra
                
                row.Cells.Add(CreateTextCell(item.IsCircled ? "X" : "")); // Circle
                
                 if (type == ReportType.Video)
                {
                     // Thumbnail Image
                     if (!string.IsNullOrEmpty(item.ThumbnailPath) && System.IO.File.Exists(item.ThumbnailPath))
                     {
                         var img = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(item.ThumbnailPath)), Height = 30 };
                         row.Cells.Add(new TableCell(new BlockUIContainer(img)) { Padding = new Thickness(2) });
                     }
                     else
                     {
                         row.Cells.Add(CreateTextCell(""));
                     }
                }

                row.Cells.Add(CreateTextCell(item.Scene));
                row.Cells.Add(CreateTextCell(item.Take));
                
                // Monospace for TC
                var tcCell = CreateTextCell(item.StartTimeCode);
                tcCell.FontFamily = new FontFamily("Consolas");
                row.Cells.Add(tcCell);

                row.Cells.Add(CreateTextCell(item.Duration));
                row.Cells.Add(CreateTextCell(item.Filename));
                
                if (type == ReportType.Audio)
                {
                    row.Cells.Add(CreateTextCell(item.Tracks));
                    row.Cells.Add(CreateTextCell(item.SampleRate));
                }
                else
                {
                    row.Cells.Add(CreateTextCell(item.Resolution));
                    row.Cells.Add(CreateTextCell(item.Codec));
                }
                
                row.Cells.Add(CreateTextCell(item.ItemNotes));

                itemsGroup.Rows.Add(row);
                index++;
            }

            itemsTable.RowGroups.Add(itemsGroup);
            doc.Blocks.Add(itemsTable);

            // Footer
            var footer = new Paragraph(new Run($"Generated by Veriflow - {DateTime.Now}"))
            {
                FontSize = 9,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0),
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = Brushes.LightGray,
                Padding = new Thickness(0, 5, 0, 0)
            };
            doc.Blocks.Add(footer);

            return doc;
        }

        // Helper for Header Info
        private TableCell CreateHeaderInfoCell(string label, string value)
        {
            var p = new Paragraph();
            p.Inlines.Add(new Run(label.ToUpper() + ": ") { Foreground = Brushes.LightGray, FontSize = 9 });
            p.Inlines.Add(new Run(value ?? "") { FontWeight = FontWeights.Bold, FontSize = 11 });
            return new TableCell(p);
        }

        private TableCell CreateTextCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text ?? "")) { Margin = new Thickness(0, 2, 0, 2) })
            {
                Padding = new Thickness(4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }
    }
}

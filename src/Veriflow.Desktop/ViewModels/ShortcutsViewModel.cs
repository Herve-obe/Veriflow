using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Veriflow.Desktop.Models;

namespace Veriflow.Desktop.ViewModels
{
    public partial class ShortcutsViewModel : ObservableObject
    {
        public ObservableCollection<ShortcutInfo> Shortcuts { get; } = new();

        public ShortcutsViewModel()
        {
            LoadShortcuts();
        }

        private void LoadShortcuts()
        {
            // Global Shortcuts
            Shortcuts.Add(new ShortcutInfo("Global", "F1", "Go to Secure Copy page"));
            Shortcuts.Add(new ShortcutInfo("Global", "F2", "Go to Media page"));
            Shortcuts.Add(new ShortcutInfo("Global", "F3", "Go to Player page"));
            Shortcuts.Add(new ShortcutInfo("Global", "F4", "Go to Sync page"));
            Shortcuts.Add(new ShortcutInfo("Global", "F5", "Go to Transcode page"));
            Shortcuts.Add(new ShortcutInfo("Global", "F6", "Go to Report page"));
            Shortcuts.Add(new ShortcutInfo("Global", "Ctrl+Tab", "Toggle Audio/Video profile"));
            Shortcuts.Add(new ShortcutInfo("Global", "F12", "Open Help Manual"));

            // Session Management
            Shortcuts.Add(new ShortcutInfo("Session", "Ctrl+N", "New Session"));
            Shortcuts.Add(new ShortcutInfo("Session", "Ctrl+O", "Open Session"));
            Shortcuts.Add(new ShortcutInfo("Session", "Ctrl+S", "Save Session"));
            Shortcuts.Add(new ShortcutInfo("Session", "Ctrl+Shift+S", "Save Session As"));

            // Edit
            Shortcuts.Add(new ShortcutInfo("Edit", "Ctrl+Z", "Undo"));
            Shortcuts.Add(new ShortcutInfo("Edit", "Ctrl+Y", "Redo"));
            Shortcuts.Add(new ShortcutInfo("Edit", "Ctrl+X", "Cut"));
            Shortcuts.Add(new ShortcutInfo("Edit", "Ctrl+C", "Copy"));
            Shortcuts.Add(new ShortcutInfo("Edit", "Ctrl+V", "Paste"));
            Shortcuts.Add(new ShortcutInfo("Edit", "Delete", "Delete selected item"));

            // Player - Playback
            Shortcuts.Add(new ShortcutInfo("Player", "Space", "Play/Pause"));
            Shortcuts.Add(new ShortcutInfo("Player", "Enter", "Stop (return to start)"));

            // Player - Navigation
            Shortcuts.Add(new ShortcutInfo("Player", "← / →", "Frame-by-frame (hold for jog)"));
            Shortcuts.Add(new ShortcutInfo("Player", "↑ / ↓", "Jump forward/backward 1 second"));
            Shortcuts.Add(new ShortcutInfo("Player", "B / N", "Previous/Next file in folder"));
            Shortcuts.Add(new ShortcutInfo("Player", "Home", "Go to start"));
            Shortcuts.Add(new ShortcutInfo("Player", "End", "Go to end"));

            // Player - Clip Logging (Video only)
            Shortcuts.Add(new ShortcutInfo("Logging", "I", "Mark In point"));
            Shortcuts.Add(new ShortcutInfo("Logging", "O", "Mark Out point"));
            Shortcuts.Add(new ShortcutInfo("Logging", "T", "Tag clip (create from In/Out)"));

            // Application
            Shortcuts.Add(new ShortcutInfo("Application", "Alt+F4", "Exit Application"));
            Shortcuts.Add(new ShortcutInfo("Application", "Esc", "Cancel/Close dialog"));
        }

        [RelayCommand]
        private void OpenFullManual()
        {
            try
            {
                var helpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Help", "UserGuide.html");
                if (File.Exists(helpPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = helpPath,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
                // Silently fail if help file not found
            }
        }
    }
}

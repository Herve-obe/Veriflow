using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Veriflow.Core.Models;
using Veriflow.Core.Services;

namespace Veriflow.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for managing sessions (New, Open, Save)
    /// </summary>
    public partial class SessionViewModel : ObservableObject, IDisposable
    {
        private readonly SessionService _sessionService;
        private readonly Func<Session> _captureStateCallback;
        private readonly Action<Session> _restoreStateCallback;
        private readonly DispatcherTimer _autoSaveTimer;
        private const int DefaultAutoSaveIntervalMinutes = 5;

        [ObservableProperty]
        private string? _currentSessionPath;

        [ObservableProperty]
        private bool _isSessionModified;

        [ObservableProperty]
        private string _currentSessionName = "Untitled Session";

        public SessionViewModel(
            Func<Session> captureStateCallback,
            Action<Session> restoreStateCallback)
        {
            _sessionService = new SessionService();
            _captureStateCallback = captureStateCallback ?? throw new ArgumentNullException(nameof(captureStateCallback));
            _restoreStateCallback = restoreStateCallback ?? throw new ArgumentNullException(nameof(restoreStateCallback));
            
            // Initialize auto-save timer
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(DefaultAutoSaveIntervalMinutes)
            };
            _autoSaveTimer.Tick += OnAutoSaveTick;
            
            // Start timer if auto-save is enabled
            var settings = Services.SettingsService.Instance.GetSettings();
            if (settings.EnableAutoSave)
            {
                _autoSaveTimer.Interval = TimeSpan.FromMinutes(settings.AutoSaveIntervalMinutes);
                _autoSaveTimer.Start();
            }
        }

        /// <summary>
        /// Creates a new session (clears current state)
        /// </summary>
        [RelayCommand]
        private void NewSession()
        {
            // Prompt to save if modified
            if (IsSessionModified)
            {
                var result = MessageBox.Show(
                    "Do you want to save changes to the current session?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _ = SaveSession(); // Fire and forget - user confirmed save
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return; // User cancelled
                }
            }

            // Create and restore new empty session
            var newSession = _sessionService.CreateNewSession();
            _restoreStateCallback(newSession);

            CurrentSessionPath = null;
            CurrentSessionName = "Untitled Session";
            IsSessionModified = false;
        }

        /// <summary>
        /// Opens an existing session from file
        /// </summary>
        [RelayCommand]
        private async Task OpenSession()
        {
            // Prompt to save if modified
            if (IsSessionModified)
            {
                var result = MessageBox.Show(
                    "Do you want to save changes to the current session?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _ = SaveSession(); // Fire and forget - user confirmed save
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            // Show open file dialog
            var dialog = new OpenFileDialog
            {
                Title = "Open Veriflow Session",
                Filter = "Veriflow Session Files (*.vfsession)|*.vfsession|All Files (*.*)|*.*",
                DefaultExt = ".vfsession"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var session = await _sessionService.LoadSessionAsync(dialog.FileName);
                    _restoreStateCallback(session);

                    CurrentSessionPath = dialog.FileName;
                    CurrentSessionName = session.SessionName;
                    IsSessionModified = false;

                    MessageBox.Show(
                        $"Session '{session.SessionName}' loaded successfully.",
                        "Session Loaded",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to load session:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Saves the current session
        /// </summary>
        [RelayCommand]
        private async Task SaveSession()
        {
            if (string.IsNullOrEmpty(CurrentSessionPath))
            {
                await SaveSessionAs();
                return;
            }

            await SaveSessionToPath(CurrentSessionPath);
        }

        /// <summary>
        /// Saves the current session with a new name
        /// </summary>
        [RelayCommand]
        private async Task SaveSessionAs()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Veriflow Session",
                Filter = "Veriflow Session Files (*.vfsession)|*.vfsession|All Files (*.*)|*.*",
                DefaultExt = ".vfsession",
                FileName = CurrentSessionName
            };

            if (dialog.ShowDialog() == true)
            {
                await SaveSessionToPath(dialog.FileName);
            }
        }

        /// <summary>
        /// Internal method to save to a specific path
        /// </summary>
        private async Task SaveSessionToPath(string filePath)
        {
            try
            {
                var session = _captureStateCallback();
                session.SessionName = Path.GetFileNameWithoutExtension(filePath);

                await _sessionService.SaveSessionAsync(session, filePath);

                CurrentSessionPath = filePath;
                CurrentSessionName = session.SessionName;
                IsSessionModified = false;

                MessageBox.Show(
                    $"Session '{session.SessionName}' saved successfully.",
                    "Session Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save session:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Marks the session as modified
        /// </summary>
        public void MarkAsModified()
        {
            IsSessionModified = true;
        }
        
        // ========================================================================
        // AUTO-SAVE
        // ========================================================================
        
        /// <summary>
        /// Event raised when auto-save completes successfully
        /// </summary>
        public event EventHandler? OnAutoSaveCompleted;
        
        private async void OnAutoSaveTick(object? sender, EventArgs e)
        {
            var settings = Services.SettingsService.Instance.GetSettings();
            
            // Check if auto-save is still enabled
            if (!settings.EnableAutoSave)
            {
                _autoSaveTimer.Stop();
                return;
            }
            
            // Only auto-save if session is modified
            if (!IsSessionModified)
                return;
            
            await AutoSaveSession();
        }
        
        private async Task AutoSaveSession()
        {
            try
            {
                string savePath;
                
                // If session has a path, save there
                if (!string.IsNullOrEmpty(CurrentSessionPath))
                {
                    savePath = CurrentSessionPath;
                }
                else
                {
                    // Auto-generate path for untitled sessions
                    var settings = Services.SettingsService.Instance.GetSettings();
                    var autoSaveFolder = Path.Combine(settings.DefaultSessionFolder, "AutoSave");
                    Directory.CreateDirectory(autoSaveFolder);
                    
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    savePath = Path.Combine(autoSaveFolder, $"AutoSave_{timestamp}.vfsession");
                }
                
                var session = _captureStateCallback();
                session.SessionName = Path.GetFileNameWithoutExtension(savePath);
                
                await _sessionService.SaveSessionAsync(session, savePath);
                
                // Update current path if it was auto-generated
                if (string.IsNullOrEmpty(CurrentSessionPath))
                {
                    CurrentSessionPath = savePath;
                    CurrentSessionName = session.SessionName;
                }
                
                IsSessionModified = false;
                
                // Raise event for notification
                OnAutoSaveCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-save failed: {ex.Message}");
                // Don't show error to user - auto-save is background operation
            }
        }
        
        /// <summary>
        /// Starts the auto-save timer
        /// </summary>
        public void StartAutoSave()
        {
            var settings = Services.SettingsService.Instance.GetSettings();
            _autoSaveTimer.Interval = TimeSpan.FromMinutes(settings.AutoSaveIntervalMinutes);
            _autoSaveTimer.Start();
        }
        
        /// <summary>
        /// Stops the auto-save timer
        /// </summary>
        public void StopAutoSave()
        {
            _autoSaveTimer.Stop();
        }
        
        /// <summary>
        /// Updates the auto-save interval
        /// </summary>
        public void UpdateAutoSaveInterval(int minutes)
        {
            _autoSaveTimer.Interval = TimeSpan.FromMinutes(minutes);
        }
        
        public void Dispose()
        {
            _autoSaveTimer?.Stop();
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Tick -= OnAutoSaveTick;
            }
        }
    }
}

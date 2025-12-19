using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Veriflow.Core.Models;
using Veriflow.Core.Services;

namespace Veriflow.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for managing sessions (New, Open, Save)
    /// </summary>
    public partial class SessionViewModel : ObservableObject
    {
        private readonly SessionService _sessionService;
        private readonly Func<Session> _captureStateCallback;
        private readonly Action<Session> _restoreStateCallback;

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
                    SaveSession();
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
                    SaveSession();
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
    }
}

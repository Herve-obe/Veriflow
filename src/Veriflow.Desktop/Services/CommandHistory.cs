using System.Collections.Generic;
using Veriflow.Desktop.Commands;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// Service for managing command history (undo/redo)
    /// </summary>
    public class CommandHistory
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();
        private const int MaxHistorySize = 50;

        /// <summary>
        /// Event raised when undo/redo state changes
        /// </summary>
        public event System.EventHandler? StateChanged;

        /// <summary>
        /// Can undo the last command
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Can redo the last undone command
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Get description of next undo command
        /// </summary>
        public string? UndoDescription => CanUndo ? _undoStack.Peek().Description : null;

        /// <summary>
        /// Get description of next redo command
        /// </summary>
        public string? RedoDescription => CanRedo ? _redoStack.Peek().Description : null;

        /// <summary>
        /// Execute a command and add it to history
        /// </summary>
        public void ExecuteCommand(IUndoableCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear(); // Clear redo stack when new command is executed

            // Limit history size
            if (_undoStack.Count > MaxHistorySize)
            {
                var tempStack = new Stack<IUndoableCommand>();
                for (int i = 0; i < MaxHistorySize; i++)
                {
                    tempStack.Push(_undoStack.Pop());
                }
                _undoStack.Clear();
                while (tempStack.Count > 0)
                {
                    _undoStack.Push(tempStack.Pop());
                }
            }

            OnStateChanged();
        }

        /// <summary>
        /// Undo the last command
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            OnStateChanged();
        }

        /// <summary>
        /// Redo the last undone command
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);

            OnStateChanged();
        }

        /// <summary>
        /// Clear all history
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnStateChanged();
        }

        private void OnStateChanged()
        {
            StateChanged?.Invoke(this, System.EventArgs.Empty);
        }
    }
}

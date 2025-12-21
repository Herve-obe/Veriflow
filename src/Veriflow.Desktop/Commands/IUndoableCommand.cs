namespace Veriflow.Desktop.Commands
{
    /// <summary>
    /// Interface for undoable commands
    /// </summary>
    public interface IUndoableCommand
    {
        /// <summary>
        /// Execute the command
        /// </summary>
        void Execute();

        /// <summary>
        /// Undo the command
        /// </summary>
        void Undo();

        /// <summary>
        /// Description of the command for UI display
        /// </summary>
        string Description { get; }
    }
}

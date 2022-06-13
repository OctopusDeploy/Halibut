namespace Halibut.Transport
{
    /// <summary>
    /// Represents a buffer that can seek backwards over previously read elements.
    /// </summary>
    interface IRewindableBuffer
    {
        /// <summary>
        /// Signal that the rewind buffer should be written to.
        /// </summary>
        void StartRewindBuffer();

        /// <summary>
        /// Stop new elements being written to the rewind buffer, and seek backwards.
        /// </summary>
        /// <param name="rewindCount">Units to seek backwards.</param>
        void FinishRewindBuffer(long rewindCount);

        /// <summary>
        /// Stop writing elements to the rewind buffer. Do not seek backwards.
        /// </summary>
        void CancelRewindBuffer();
    }
}
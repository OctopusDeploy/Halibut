using System;

namespace Halibut
{
    interface ICanBeSwitchedToNotReportProgress
    {
        /// <summary>
        /// Used by the Redis PRQ, so that if the DataStream is stored in a shared location we
        /// do NOT report the file upload progress as the progress in writing to that shared location.
        /// We instead want to report on the progress of sending the data to the Polling Service.
        /// </summary>
        void SwitchWriterToNotReportProgress();
    }
}
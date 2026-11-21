// YaesuAccScope: reads a Yaesu FTdx101's own scope data from its ACC socket.
// Copyright (C) 2026 Colin Campbell MM5AGM
// SPDX-License-Identifier: LGPL-3.0-only (see COPYING.LESSER and COPYING)

namespace YaesuAccScope
{
    /// <summary>
    /// Keeps a stream of frames on one radio's layout. A frame says which radio
    /// sent it from its layout alone (see <see cref="AccFrame.Radio"/>), so one
    /// garbled frame could read as the other radio and flip the display for a
    /// frame. This takes the first frame's answer at once, then changes only
    /// when <see cref="FramesToChange"/> frames in a row agree on the other
    /// radio: a fifth of a second at 25 frames a second.
    /// </summary>
    public sealed class AccRadioSettler
    {
        /// <summary>Frames in a row that must read as the other radio before the layout changes.</summary>
        public const int FramesToChange = 5;

        private AccRadio? _settled;
        private int _against;

        /// <summary>The radio the stream is on, or null before the first frame.</summary>
        public AccRadio? Radio => _settled;

        /// <summary>Takes the radio one frame reads as, and returns the radio to decode it as.</summary>
        public AccRadio Settle(AccRadio seen)
        {
            if (_settled == null || seen == _settled)
            {
                _settled = seen;
                _against = 0;
            }
            else if (++_against >= FramesToChange)
            {
                _settled = seen;
                _against = 0;
            }
            return _settled.Value;
        }

        /// <summary>Forgets the radio, for a new connection.</summary>
        public void Reset()
        {
            _settled = null;
            _against = 0;
        }
    }
}

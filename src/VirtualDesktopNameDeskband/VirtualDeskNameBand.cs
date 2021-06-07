using SharpShell.Attributes;
using SharpShell.SharpDeskBand;
using System.Drawing;
using System.Runtime.InteropServices;

namespace VirtualDesktopNameDeskband
{
    [ComVisible(true)]
    [DisplayName("Virtual Desktop Manager")]
    public class VirtualDeskNameBand : SharpDeskBand
    {
        private DeskbandControl deskbandControl;

        protected override System.Windows.Forms.UserControl CreateDeskBand() => deskbandControl ?? (deskbandControl = new DeskbandControl());

        protected override BandOptions GetBandOptions() => new BandOptions
        {
            HasVariableHeight = false,
            IsSunken = false,
            ShowTitle = true,
            Title = "Virtual Desktop Manager",
            UseBackgroundColour = false,
            AlwaysShowGripper = false,
            IsFixed = true
        };

        protected override void OnBandRemoved()
        {
            deskbandControl?.Close();

            base.OnBandRemoved();
        }

        protected override Size GetMaximumSize()
        {
            return deskbandControl.GetPreferredSize();
        }

        protected override Size GetMinimumSize()
        {
            return deskbandControl.GetPreferredSize();
        }
    }
}
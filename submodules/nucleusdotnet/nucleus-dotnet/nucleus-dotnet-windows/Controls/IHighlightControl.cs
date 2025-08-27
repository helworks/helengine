#if WINDOWS
namespace Nucleus.Platform.Windows.Controls {
    public interface IRadioControl {
        void RadioSelected();
        void RadioUnselected();

        // mouse enter basically
        void UserOver();
        // mouse leave
        void UserLeave();

        bool EnableClicking { get; set; }

        //void Highlight();
        //void SoftHighlight();
        //void Darken();
    }
}
#endif
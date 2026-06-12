using System.Security.Cryptography;
using System.Text;
using System.Windows;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace QuickKill;

public partial class AttributionPasswordDialog : Window
{
    // SHA-256 of "263153@Maj" — attribution cannot be changed without this key.
    private const string CorrectHash = "4b5328ff06f7ec3597d76e38f06444f7cb1d763d6c77e46f57a5edaed1c3c9ef";

    public AttributionPasswordDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => PwdBox.Focus();
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e) => Verify();
    private void CancelBtn_Click(object sender, RoutedEventArgs e)  => Close();

    private void PwdBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  { Verify(); e.Handled = true; }
        if (e.Key == Key.Escape) { Close();  e.Handled = true; }
    }

    private void Verify()
    {
        var input = PwdBox.Password;
        var hash  = ComputeHash(input);

        if (hash == CorrectHash)
        {
            StatusLabel.Text      = "✓  Verified — you are the original developer.";
            StatusLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x34, 0xC7, 0x59));
            ConfirmBtn.IsEnabled = false;
        }
        else
        {
            StatusLabel.Text      = "✗  Wrong password. Attribution is protected.";
            StatusLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xFF, 0x45, 0x3A));
            PwdBox.Clear();
            PwdBox.Focus();
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash  = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

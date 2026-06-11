namespace MailMessenger.Client.Views;

/// <summary>
/// On wide screens (≥ 600 dp width) this page shows the ContactsPage sidebar
/// permanently on the left. On narrow screens the page is unused — the normal
/// Shell push-navigation handles Contacts → Chat.
/// </summary>
public partial class MainSplitPage : ContentPage
{
	private const double SidebarBreakpoint = 600;

	private readonly ContactsPage contactsPage;

	public MainSplitPage(ContactsPage contactsPage)
	{
		InitializeComponent();
		this.contactsPage = contactsPage;

		// Embed the ContactsPage as a child view inside the sidebar column
		SidebarColumn.Children.Add(contactsPage.Content);
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);

		var isWide = width >= SidebarBreakpoint;

		// Show/hide sidebar
		SidebarColumn.IsVisible = isWide;
		SidebarDivider.IsVisible = isWide;

		// On wide layout, hide the back button hint and show placeholder
		NoChatPlaceholder.IsVisible = isWide;

		// Adjust column widths
		if (isWide)
		{
			RootGrid.ColumnDefinitions[0] = new ColumnDefinition(new GridLength(380, GridUnitType.Absolute));
			RootGrid.ColumnDefinitions[1] = new ColumnDefinition(GridLength.Star);
		}
		else
		{
			RootGrid.ColumnDefinitions[0] = new ColumnDefinition(new GridLength(0, GridUnitType.Absolute));
			RootGrid.ColumnDefinitions[1] = new ColumnDefinition(GridLength.Star);
		}
	}
}

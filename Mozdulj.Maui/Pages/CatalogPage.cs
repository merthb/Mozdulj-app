using Microsoft.Maui.ApplicationModel;
using System.Collections.ObjectModel;
using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// Native virtualized library. Never put the CollectionView in a ScrollView or a
/// StackLayout with unbounded height: Android would measure/render every pose.
/// Filters scan lightweight metadata; cards and pose renderers are only created
/// for visible rows. RemainingItemsThreshold appends 12 more on demand.
/// </summary>
public sealed class CatalogPage : ContentPage
{
    private const int BatchSize = 12;
    private readonly AppState _state;
    private readonly SearchBar _search = new() { Placeholder = "Gyakorlat vagy testtáj…" };
    private readonly Picker _category = new() { Title = "Mozgástípus" };
    private readonly Picker _region = new() { Title = "Érintett testtáj" };
    private readonly Picker _origin = new() { Title = "Forrás / állapot" };
    private readonly Switch _favoritesOnly = new();
    private readonly Switch _availableOnly = new();
    private readonly ObservableCollection<LibraryRow> _visible = [];
    private readonly CollectionView _items;
    private readonly Label _counter = UI.Label("Gyakorlatok betöltése…", 13, color:UI.Muted);
    private List<LibraryRow> _matches = [];
    private List<LibraryRow> _librarySnapshot = [];
    private bool _refreshing;
    private bool _loading;
    private CancellationTokenSource? _searchDelay;
    private int _snapshotRevision = -1;
    private bool _snapshotReady;
    private bool _presented;
    private HashSet<string> _lastFavorites = new(StringComparer.Ordinal);
    private HashSet<string> _lastEquipment = new(StringComparer.Ordinal);

    public CatalogPage(AppState state)
    {
        _state = state;
        UI.Setup(this, "Gyakorlattár");
        foreach (var option in new[] { "Minden gyakorlat", "Alapgyakorlatok", "Saját gyakorlatok", "Piszkozatok", "Kikapcsolt" })
            _origin.Items.Add(option);
        _origin.SelectedIndex = 0;
        _search.TextChanged += (_,_) => DebounceSearch();
        _category.SelectedIndexChanged += (_,_) => RefreshMatches();
        _region.SelectedIndexChanged += (_,_) => RefreshMatches();
        _origin.SelectedIndexChanged += (_,_) => RefreshMatches();
        _favoritesOnly.Toggled += (_,_) => RefreshMatches();
        _availableOnly.Toggled += (_,_) => RefreshMatches();

        _items = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemsSource = _visible,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 },
            // Each item must be measured: a multi-line name or extra action changes row height.
            ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
            ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepScrollOffset,
            RemainingItemsThreshold = 4,
            ItemTemplate = new DataTemplate(() =>
            {
                // DataTemplate is realized only for the visible native rows.
                var host = new ContentView { Padding = new Thickness(14, 0) };
                host.BindingContextChanged += (_,_) =>
                {
                    if (host.BindingContext is LibraryRow row)
                        host.Content = BuildCard(row);
                };
                return host;
            })
        };
        _items.RemainingItemsThresholdReached += (_,_) => LoadNextBatch();

        var header = new VerticalStackLayout { Padding = new Thickness(18,16,18,8), Spacing = 9 };
        header.Children.Add(UI.Label("Gyakorlattár", 25, true));

        var create = UI.Button("+ Új saját gyakorlat");
        create.Clicked += async (_,_) => await Navigation.PushModalAsync(new NavigationPage(new ExerciseEditorPage(_state))
            { BarBackgroundColor=UI.Background, BarTextColor=UI.Ink });
        header.Children.Add(create);
        header.Children.Add(_search);
        var toggleFilters = UI.Button("⚙ További szűrők", false);
        toggleFilters.FontSize=13; toggleFilters.HeightRequest=38;
        var filterPanel = new VerticalStackLayout { Spacing=7, IsVisible=false };
        // Source is a frequently-used filter: keep it visible without expanding a tall panel.
        var quickSources = new Grid { ColumnSpacing=5, ColumnDefinitions = {
            new ColumnDefinition { Width=GridLength.Star }, new ColumnDefinition { Width=GridLength.Star },
            new ColumnDefinition { Width=GridLength.Star } } };
        var sourceButtons = new [] { UI.Button("Mind",false),UI.Button("Alap",false),UI.Button("Saját",false) };
        for(int i=0;i<sourceButtons.Length;i++)
        {
            int idx=i;Grid.SetColumn(sourceButtons[i],i);quickSources.Children.Add(sourceButtons[i]);
            sourceButtons[i].Clicked += (_,_) => _origin.SelectedIndex=idx;
            sourceButtons[i].HeightRequest=38;
            sourceButtons[i].FontSize=13;
        }
        void SyncSourceButtons()
        {
            for(int i=0;i<3;i++)
            {
                var active=_origin.SelectedIndex==i;
                sourceButtons[i].BackgroundColor=active ? UI.PrimaryButton : UI.Lavender;
                sourceButtons[i].TextColor=active ? Colors.White : UI.Purple;
            }
        }
        _origin.SelectedIndexChanged+=(_,_)=>SyncSourceButtons();
        SyncSourceButtons();
        filterPanel.Children.Add(quickSources);
        var filterGrid = new Grid { ColumnSpacing=8,RowSpacing=3,
            ColumnDefinitions={new ColumnDefinition {Width=GridLength.Star},
                               new ColumnDefinition {Width=GridLength.Star}},
            RowDefinitions={new RowDefinition {Height=GridLength.Auto},
                            new RowDefinition {Height=GridLength.Auto},
                            new RowDefinition {Height=GridLength.Auto},
                            new RowDefinition {Height=GridLength.Auto}}};
        void Cell(View view,int column,int row) {Grid.SetColumn(view,column);Grid.SetRow(view,row);filterGrid.Children.Add(view);}
        Cell(UI.Label("Mozgástípus",12,true,color:UI.Muted),0,0);
        Cell(UI.Label("Érintett testtáj",12,true,color:UI.Muted),1,0);
        Cell(_category,0,1);Cell(_region,1,1);
        Cell(UI.Label("Forrás és állapot",12,true,color:UI.Muted),0,2);
        Cell(_origin,0,3);
        filterPanel.Children.Add(filterGrid);
        var switches=new HorizontalStackLayout {Spacing=12};
        switches.Children.Add(FilterRow(_availableOnly,"Elérhető eszköz"));
        switches.Children.Add(FilterRow(_favoritesOnly,"Kedvenc"));
        filterPanel.Children.Add(switches);
        toggleFilters.Clicked += (_,_) =>
        {
            filterPanel.IsVisible = !filterPanel.IsVisible;
            toggleFilters.Text=filterPanel.IsVisible ? "⚙ Szűrők bezárása" : "⚙ További szűrők";
        };
        header.Children.Add(toggleFilters);
        header.Children.Add(filterPanel);
        header.Children.Add(_counter);

        // Header participates in the CollectionView layout. An overlaid header used
        // to cover the first cards and caused ScrollTo(0) to stop underneath filters.
        // Keep the filter content INSIDE the list, so card #1 is always visible.
        _items.Header = header;
        var layout = new Grid();
        layout.Children.Add(_items);
        // Accessible floating shortcut; stays outside the virtualized list.
        var jumpTop = UI.Button("↑ Tetejére", false);
        jumpTop.FontSize = 13;
        jumpTop.HeightRequest = 40;
        jumpTop.Padding = new Thickness(14, 5);
        jumpTop.IsVisible = false;
        jumpTop.HorizontalOptions = LayoutOptions.End;
        jumpTop.VerticalOptions = LayoutOptions.End;
        jumpTop.Margin = new Thickness(0, 0, 18, 22);
        jumpTop.ZIndex = 8;
        jumpTop.Clicked += (_, _) => _items.ScrollTo(0, position:ScrollToPosition.Start, animate:true);
        _items.Scrolled += (_, e) => jumpTop.IsVisible = e.VerticalOffset > 550;
        layout.Children.Add(jumpTop);
        Content = layout;
    }

    private static HorizontalStackLayout FilterRow(Switch toggle, string title)
    {
        toggle.Scale=0.75;
        return new() { Spacing=2, Children={toggle,UI.Label(title,12)} };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // NavigationPage keeps this CatalogPage instance alive. When returning from
        // a detail/3D screen, do not clear the CollectionView or reset its scroll.
        // A revision/favorite change only reconciles the affected visible rows.
        var libraryChanged = EnsureLibrarySnapshot();
        if (!_presented || libraryChanged || !_lastFavorites.SetEquals(_state.Data.FavoriteIds) ||
            (_availableOnly.IsToggled && !_lastEquipment.SetEquals(_state.Preferences.AvailableEquipment)))
        {
            RefreshMatches(resetScroll: !_presented);
            _presented = true;
        }
    }

    private bool EnsureLibrarySnapshot()
    {
        // AppState increments LibraryRevision for edits, imports, enable/disable,
        // and archiving. Favorite changes are tracked separately below.
        if (_snapshotReady && _snapshotRevision == _state.Data.LibraryRevision)
            return false;

        _librarySnapshot = _state.LibraryItems
            .Select(x => new LibraryRow(x.Exercise, x.BuiltIn, x.Status)).ToList();
        _snapshotRevision = _state.Data.LibraryRevision;
        _snapshotReady = true;

        // Preserve the selected filters and avoid needlessly resetting pickers
        // every time we return to the screen.
        _refreshing = true;
        try
        {
            UpdatePickerItems(_category,
                ["Összes mozgástípus", .._librarySnapshot.Select(x => x.Exercise.Category)
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)]);
            UpdatePickerItems(_region,
                ["Összes testtáj", .._librarySnapshot.SelectMany(x => x.Exercise.Regions)
                    .Distinct().OrderBy(x => x)]);
        }
        finally { _refreshing = false; }
        return true;
    }

    private static void UpdatePickerItems(Picker picker, IReadOnlyList<string> values)
    {
        if (picker.Items.Count == values.Count &&
            !picker.Items.Where((value, index) => value != values[index]).Any())
            return;
        var previouslySelected = picker.SelectedItem?.ToString();
        picker.Items.Clear();
        foreach (var value in values) picker.Items.Add(value);
        picker.SelectedIndex = Math.Max(0, picker.Items.IndexOf(previouslySelected ?? values[0]));
    }

    private async void DebounceSearch()
    {
        _searchDelay?.Cancel();
        var token = (_searchDelay = new CancellationTokenSource()).Token;
        try
        {
            await Task.Delay(180,token);
            if (!token.IsCancellationRequested) MainThread.BeginInvokeOnMainThread(() => RefreshMatches());
        }
        catch (TaskCanceledException) { }
    }

    private void RefreshMatches(bool resetScroll = true)
    {
        if (_refreshing) return;
        var query = _search.Text?.Trim() ?? "";
        var category = _category.SelectedItem?.ToString() ?? "Összes mozgástípus";
        var region = _region.SelectedItem?.ToString() ?? "Összes testtáj";
        var source = _origin.SelectedItem?.ToString() ?? "Minden gyakorlat";
        var favorites = _state.Data.FavoriteIds.ToHashSet(StringComparer.Ordinal);
        var changedFavorites = new HashSet<string>(_lastFavorites, StringComparer.Ordinal);
        changedFavorites.SymmetricExceptWith(favorites);
        var equipment = _state.Preferences.AvailableEquipment.ToHashSet(StringComparer.Ordinal);
        _matches = _librarySnapshot.Where(x =>
            (category == "Összes mozgástípus" || x.Exercise.Category == category) &&
            (region == "Összes testtáj" || x.Exercise.Regions.Contains(region)) &&
            (query.Length == 0 || x.Exercise.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                x.Exercise.Regions.Any(r => r.Contains(query, StringComparison.CurrentCultureIgnoreCase))) &&
            (!_favoritesOnly.IsToggled || favorites.Contains(x.Exercise.Id)) &&
            (!_availableOnly.IsToggled || x.Exercise.Equipment.All(equipment.Contains)) &&
            (source == "Minden gyakorlat" ||
                (source == "Alapgyakorlatok" && x.BuiltIn) ||
                (source == "Saját gyakorlatok" && !x.BuiltIn) ||
                (source == "Piszkozatok" && x.Status == LibraryStatuses.Draft) ||
                (source == "Kikapcsolt" && x.Status == LibraryStatuses.Disabled)))
            .OrderBy(x => x.Exercise.Category).ThenBy(x => x.Exercise.Name)
            .ToList();

        if (resetScroll)
        {
            // A deliberate search/filter change starts a new result set.
            _visible.Clear();
            _items.RemainingItemsThreshold = 4;
            LoadNextBatch();
            if (_visible.Count > 0)
                _items.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
        }
        else
        {
            // Toggle, favorite and returning from an exercise detail must NOT
            // clear the list: only insert/remove/replace the affected cards.
            // Retain the number of already loaded items when possible.
            ReconcileVisibleRows(changedFavorites);
        }
        _lastFavorites = favorites;
        _lastEquipment = equipment;
        _counter.Text = $"{_matches.Count} találat";
        _items.RemainingItemsThreshold = _visible.Count < _matches.Count ? 4 : -1;
    }

    private void ReconcileVisibleRows(HashSet<string> changedFavorites)
    {
        var count = Math.Max(BatchSize, _visible.Count);
        var desired = _matches.Take(count).ToList();
        var desiredIds = desired.Select(x => x.Exercise.Id).ToHashSet(StringComparer.Ordinal);
        int index = 0;
        while (index < desired.Count)
        {
            var next = desired[index];
            if (index >= _visible.Count)
            {
                _visible.Add(next);
                index++;
                continue;
            }
            var current = _visible[index];
            if (current.Exercise.Id == next.Exercise.Id)
            {
                if (current.Status != next.Status || current.BuiltIn != next.BuiltIn ||
                    !ReferenceEquals(current.Exercise, next.Exercise) ||
                    changedFavorites.Contains(next.Exercise.Id))
                    _visible[index] = next; // one native card changes, not the list
                index++;
            }
            else if (!desiredIds.Contains(current.Exercise.Id))
            {
                _visible.RemoveAt(index); // an item was removed by an active filter
            }
            else
            {
                _visible.Insert(index, next); // added or reordered, retain other rows
                index++;
            }
        }
        while (_visible.Count > desired.Count)
            _visible.RemoveAt(_visible.Count - 1);
    }

    private void LoadNextBatch()
    {
        if (_loading) return;
        _loading=true;
        try
        {
            var start=_visible.Count;
            foreach(var item in _matches.Skip(start).Take(BatchSize)) _visible.Add(item);
            _counter.Text=$"{_matches.Count} találat";
            if(_visible.Count>=_matches.Count) _items.RemainingItemsThreshold=-1;
        }
        finally { _loading=false; }
    }

    private View BuildCard(LibraryRow row)
    {
        var exercise = row.Exercise;
        var layout = new VerticalStackLayout { Spacing=7 };
        var description = new VerticalStackLayout { Spacing=3, Children=
        {
            UI.Label(string.IsNullOrWhiteSpace(exercise.Name)?"Névtelen piszkozat":exercise.Name,15,true),
            UI.Label($"{exercise.Category} · {exercise.RegionLabel}",12,color:UI.Muted),
            UI.Label($"{(row.BuiltIn?"Alap":"Saját")} · {StatusText(row.Status)} · {exercise.TargetLabel}",11,color:UI.Teal)
        }};
        layout.Children.Add(description);
        // Tap anywhere on the card surface to open details. Secondary icon actions
        // are separate native controls and must not navigate to details.
        // Wrapping controls allows narrow screens and large Android text settings
        // without clipping the last buttons or overflowing the card bounds.
        var actions = new FlexLayout
        {
            Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlign.Center, JustifyContent = FlexJustify.Start
        };
        static Button SmallAction(string glyph,string hint)
        {
            var button = new Button {Text=glyph,FontSize=18,WidthRequest=44,
                MinimumHeightRequest=44, FontAutoScalingEnabled=true,
                Margin=new Thickness(0,0,8,8), Padding=new Thickness(0),
                CornerRadius=12,BackgroundColor=UI.Lavender,
                TextColor=UI.Purple,BorderWidth=0};
            SemanticProperties.SetDescription(button,hint);
            return button;
        }
        // 1. Enable / disable
        if(row.Status is LibraryStatuses.Active or LibraryStatuses.Disabled)
        {
            // Use a labelled toggle with packaged vector artwork, not a Unicode
            // power symbol (some Android system fonts render U+23FB as a blank).
            // The text describes the ACTION; the symbol depicts the CURRENT state.
            var isActive = row.Status == LibraryStatuses.Active;
            var toggle = new Button
            {
                Text = isActive ? "Kikapcsolás" : "Bekapcsolás",
                ImageSource = new FileImageSource
                {
                    File = isActive ? "exercise_toggle_on.png" : "exercise_toggle_off.png"
                },
                ContentLayout = new Button.ButtonContentLayout(
                    Button.ButtonContentLayout.ImagePosition.Left, 4),
                FontSize = 11.5,
                FontAttributes = FontAttributes.Bold,
                MinimumWidthRequest = 115,
                MinimumHeightRequest = 44,
                FontAutoScalingEnabled = true,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(8, 6),
                CornerRadius = 12,
                BackgroundColor = isActive ? UI.Lavender : UI.SuccessBackground,
                TextColor = isActive ? UI.Purple : UI.Teal,
                BorderWidth = 0,
                LineBreakMode = LineBreakMode.WordWrap
            };
            SemanticProperties.SetDescription(toggle, isActive
                ? "Aktív gyakorlat. Megnyomva kikapcsolható."
                : "Kikapcsolt gyakorlat. Megnyomva bekapcsolható.");
            toggle.Clicked+=async (_,_)=>
            {
                try
                {
                    if(row.BuiltIn) await _state.SetBuiltInEnabledAsync(exercise.Id,row.Status==LibraryStatuses.Disabled);
                    else await _state.SetCustomEnabledAsync(exercise.Id,row.Status==LibraryStatuses.Disabled);
                    EnsureLibrarySnapshot();
                    RefreshMatches(resetScroll: false);
                }
                catch(Exception ex){await DisplayAlertAsync("Nem sikerült",ex.Message,"Rendben");}
            };
            actions.Children.Add(toggle);
        }
        // 2. Favorite (works for both built-in and custom).
        var favorite=SmallAction(_state.IsFavorite(exercise.Id)?"♥":"♡",
            _state.IsFavorite(exercise.Id)?"Kedvenc eltávolítása":"Kedvencekhez adás");
        favorite.Clicked+=async (_,_)=>
        {
            try{await _state.ToggleFavoriteAsync(exercise.Id);RefreshMatches(resetScroll: false);}
            catch(Exception ex){await DisplayAlertAsync("Nem sikerült",ex.Message,"Rendben");}
        };
        actions.Children.Add(favorite);
        // 3. Edit, or built-in text/3D illustration correction.
        var edit=SmallAction("✎",row.BuiltIn?"Leírás és ábra javítása":"Gyakorlat szerkesztése");
        edit.Clicked+=async (_,_)=>
        {
            if(row.BuiltIn)
                await Navigation.PushModalAsync(new NavigationPage(new BuiltInContentEditorPage(_state,exercise.Id))
                    {BarBackgroundColor=UI.Background,BarTextColor=UI.Ink});
            else
                await Navigation.PushModalAsync(new NavigationPage(new ExerciseEditorPage(_state,exercise))
                    {BarBackgroundColor=UI.Background,BarTextColor=UI.Ink});
        };
        actions.Children.Add(edit);
        // Only custom items have an extra archive action. Destructive actions
        // remain explicit and never share the main card tap gesture.
        if(!row.BuiltIn)
        {
            var archive=SmallAction("⋯","Saját gyakorlat archiválása");
            archive.Clicked+=async (_,_)=>
            {
                if(!await DisplayAlertAsync("Saját gyakorlat archiválása?", "A korábbi teljesítések megmaradnak.", "Archiválás", "Mégse"))return;
                try {await _state.ArchiveCustomExerciseAsync(exercise.Id);
                    EnsureLibrarySnapshot();RefreshMatches(resetScroll: false);}
                catch(Exception ex){await DisplayAlertAsync("Nem sikerült",ex.Message,"Rendben");}
            };
            actions.Children.Add(archive);
        }
        layout.Children.Add(actions);
        var card=UI.Card(layout);
        var tap=new TapGestureRecognizer();
        tap.Tapped+=async (_,_)=>await Navigation.PushAsync(new ExerciseDetailPage(_state,exercise,fromCatalog:true));
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private static string StatusText(string status) => status switch
    {
        LibraryStatuses.Active=>"Aktív", LibraryStatuses.Disabled=>"Kikapcsolt",
        LibraryStatuses.Draft=>"Piszkozat",_=>"Archivált"
    };

    private sealed record LibraryRow(Exercise Exercise,bool BuiltIn,string Status);
}

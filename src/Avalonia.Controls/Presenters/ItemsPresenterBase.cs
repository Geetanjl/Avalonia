using System;
using System.Collections;
using System.Collections.Specialized;
using Avalonia.Collections;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Avalonia.Controls.Presenters
{
    /// <summary>
    /// Base class for controls that present items inside an <see cref="ItemsControl"/>.
    /// </summary>
    public abstract class ItemsPresenterBase : Control, IItemsPresenter, IChildIndexProvider
    {
        /// <summary>
        /// Defines the <see cref="Items"/> property.
        /// </summary>
        public static readonly DirectProperty<ItemsPresenterBase, IEnumerable?> ItemsProperty =
            ItemsControl.ItemsProperty.AddOwner<ItemsPresenterBase>(o => o.Items, (o, v) => o.Items = v);

        /// <summary>
        /// Defines the <see cref="ItemsPanel"/> property.
        /// </summary>
        public static readonly StyledProperty<ITemplate<Panel>> ItemsPanelProperty =
            ItemsControl.ItemsPanelProperty.AddOwner<ItemsPresenterBase>();

        /// <summary>
        /// Defines the <see cref="ItemTemplate"/> property.
        /// </summary>
        public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
            ItemsControl.ItemTemplateProperty.AddOwner<ItemsPresenterBase>();

        /// <summary>
        /// Defines the <see cref="DisplayMemberBinding" /> property
        /// </summary>
        public static readonly StyledProperty<IBinding?> DisplayMemberBindingProperty =
            ItemsControl.DisplayMemberBindingProperty.AddOwner<ItemsPresenterBase>();
        
        private IEnumerable? _items;
        private IDisposable? _itemsSubscription;
        private bool _createdPanel;
        private IItemContainerGenerator? _generator;
        private EventHandler<ChildIndexChangedEventArgs>? _childIndexChanged;

        /// <summary>
        /// Initializes static members of the <see cref="ItemsPresenter"/> class.
        /// </summary>
        static ItemsPresenterBase()
        {
            TemplatedParentProperty.Changed.AddClassHandler<ItemsPresenterBase>((x,e) => x.TemplatedParentChanged(e));
        }

        /// <summary>
        /// Gets or sets the items to be displayed.
        /// </summary>
        public IEnumerable? Items
        {
            get
            {
                return _items;
            }

            set
            {
                _itemsSubscription?.Dispose();
                _itemsSubscription = null;

                if (!IsHosted && _createdPanel && value is INotifyCollectionChanged incc)
                {
                    _itemsSubscription = incc.WeakSubscribe(ItemsCollectionChanged);
                }

                SetAndRaise(ItemsProperty, ref _items, value);

                if (_createdPanel)
                {
                    ItemsChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
        }

        /// <summary>
        /// Gets the item container generator.
        /// </summary>
        public IItemContainerGenerator ItemContainerGenerator
        {
            get
            {
                if (_generator == null)
                {
                    _generator = CreateItemContainerGenerator();
                }

                return _generator;
            }

            internal set
            {
                if (_generator != null)
                {
                    throw new InvalidOperationException("ItemContainerGenerator already created.");
                }

                _generator = value;
            }
        }

        /// <summary>
        /// Gets or sets a template which creates the <see cref="Panel"/> used to display the items.
        /// </summary>
        public ITemplate<Panel> ItemsPanel
        {
            get { return GetValue(ItemsPanelProperty); }
            set { SetValue(ItemsPanelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the data template used to display the items in the control.
        /// </summary>
        public IDataTemplate? ItemTemplate
        {
            get { return GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        /// <summary>
        /// Gets or sets the <see cref="IBinding"/> to use for binding to the display member of each item.
        /// </summary>
        public IBinding? DisplayMemberBinding
        {
            get { return GetValue(DisplayMemberBindingProperty); }
            set { SetValue(DisplayMemberBindingProperty, value); }
        }

        /// <summary>
        /// Gets the panel used to display the items.
        /// </summary>
        public Panel? Panel
        {
            get;
            private set;
        }

        protected bool IsHosted => TemplatedParent is IItemsPresenterHost;

        event EventHandler<ChildIndexChangedEventArgs>? IChildIndexProvider.ChildIndexChanged
        {
            add => _childIndexChanged += value;
            remove => _childIndexChanged -= value;
        }

        /// <inheritdoc/>
        public override sealed void ApplyTemplate()
        {
            if (!_createdPanel)
            {
                CreatePanel();
            }
        }

        /// <inheritdoc/>
        public virtual void ScrollIntoView(int index)
        {
        }

        /// <inheritdoc/>
        void IItemsPresenter.ItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            if (Panel != null)
            {
                ItemsChanged(e);

                _childIndexChanged?.Invoke(this, ChildIndexChangedEventArgs.Empty);
            }
        }

        /// <summary>
        /// Creates the <see cref="ItemContainerGenerator"/> for the control.
        /// </summary>
        /// <returns>
        /// An <see cref="IItemContainerGenerator"/> or null.
        /// </returns>
        protected virtual IItemContainerGenerator CreateItemContainerGenerator()
        {
            var i = TemplatedParent as ItemsControl;
            var result = i?.ItemContainerGenerator;

            if (result == null)
            {
                result = new ItemContainerGenerator(this);
                result.ItemTemplate = ItemTemplate;
                result.DisplayMemberBinding = DisplayMemberBinding;
            }

            result.Materialized += ContainerActionHandler;
            result.Dematerialized += ContainerActionHandler;
            result.Recycled += ContainerActionHandler;

            return result;
        }

        private void ContainerActionHandler(object? sender, ItemContainerEventArgs e)
        {
            for (var i = 0; i < e.Containers.Count; i++)
            {
                _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(e.Containers[i].ContainerControl));
            }
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            Panel!.Measure(availableSize);
            return Panel.DesiredSize;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            Panel!.Arrange(new Rect(finalSize));
            return finalSize;
        }

        /// <summary>
        /// Called when the <see cref="Panel"/> is created.
        /// </summary>
        /// <param name="panel">The panel.</param>
        protected virtual void PanelCreated(Panel panel)
        {
        }

        /// <summary>
        /// Called when the items for the presenter change, either because <see cref="Items"/>
        /// has been set, the items collection has been modified, or the panel has been created.
        /// </summary>
        /// <param name="e">A description of the change.</param>
        /// <remarks>
        /// The panel is guaranteed to be created when this method is called.
        /// </remarks>
        protected virtual void ItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            ItemContainerSync.ItemsChanged(this, Items, e);
        }

        /// <summary>
        /// Creates the <see cref="Panel"/> when <see cref="ApplyTemplate"/> is called for the first
        /// time.
        /// </summary>
        private void CreatePanel()
        {
            Panel = ItemsPanel.Build();
            Panel.SetValue(TemplatedParentProperty, TemplatedParent);

            LogicalChildren.Clear();
            VisualChildren.Clear();
            LogicalChildren.Add(Panel);
            VisualChildren.Add(Panel);

            _createdPanel = true;

            if (!IsHosted && _itemsSubscription == null && Items is INotifyCollectionChanged incc)
            {
                _itemsSubscription = incc.WeakSubscribe(ItemsCollectionChanged);
            }

            PanelCreated(Panel);
        }

        /// <summary>
        /// Called when the <see cref="Items"/> collection changes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void ItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_createdPanel)
            {
                ItemsChanged(e);
            }
        }

        private void TemplatedParentChanged(AvaloniaPropertyChangedEventArgs e)
        {
            (e.NewValue as IItemsPresenterHost)?.RegisterItemsPresenter(this);
        }

        int IChildIndexProvider.GetChildIndex(ILogical child)
        {
            if (child is Control control && ItemContainerGenerator is { } generator)
            {
                var index = ItemContainerGenerator.IndexFromContainer(control);

                return index;
            }

            return -1;
        }

        bool IChildIndexProvider.TryGetTotalCount(out int count)
        {
            return Items.TryGetCountFast(out count);
        }
    }
}

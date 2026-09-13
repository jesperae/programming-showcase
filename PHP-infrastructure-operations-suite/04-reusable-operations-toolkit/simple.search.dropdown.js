/*
 * Simple Search Dropdown
 * Filtered choices for compact operations forms.
 */
(function (global) {
	'use strict';

	function SimpleSearchDropdown(Options) {
		this.$Input = jQuery(Options.input);
		this.$Menu = jQuery(Options.menu);
		this.Items = Options.items || [];
		this.OnSelect = Options.onSelect || function () {};
		this.Bind();
	}

	SimpleSearchDropdown.prototype.Normalize = function (Value) {
		return String(Value || '').toUpperCase().replace(/\s+/g, ' ').trim();
	};

	SimpleSearchDropdown.prototype.SetItems = function (Items) {
		this.Items = Items || [];
		this.Render(this.$Input.val());
	};

	SimpleSearchDropdown.prototype.Filter = function (Term) {
		var Needle = this.Normalize(Term);
		return this.Items.filter(function (Item) {
			var Haystack = String(Item.search || Item.label || Item.value || '').toUpperCase();
			return !Needle || Haystack.indexOf(Needle) !== -1;
		});
	};

	SimpleSearchDropdown.prototype.Render = function (Term) {
		var Self = this;
		var Matches = this.Filter(Term);
		this.$Menu.empty();
		jQuery.each(Matches, function (_, Item) {
			jQuery('<button type="button" class="search-dropdown-option"></button>')
				.attr('data-value', Item.value || '')
				.text(Item.label || Item.value || '')
				.on('click', function () {
					Self.$Input.val(jQuery(this).attr('data-value'));
					Self.$Menu.hide();
					Self.OnSelect(Item);
				})
				.appendTo(Self.$Menu);
		});
		this.$Menu.toggle(Matches.length > 0);
	};

	SimpleSearchDropdown.prototype.Bind = function () {
		var Self = this;
		this.$Input.on('focus input', function () {
			Self.Render(this.value);
		});
		jQuery(document).on('click', function (Event) {
			if (!jQuery(Event.target).closest(Self.$Input.add(Self.$Menu)).length) {
				Self.$Menu.hide();
			}
		});
	};

	global.SimpleSearchDropdown = SimpleSearchDropdown;
})(window);


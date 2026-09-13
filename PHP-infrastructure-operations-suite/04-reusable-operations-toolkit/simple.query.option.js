/* Prompt for a value and place it in the current URL. */
jQuery(function ($) {
	'use strict';

	$(document).on('click', '[data-query-option]', function (Event) {
		Event.preventDefault();
		var $Element = $(this);
		var Key = $Element.attr('data-query-option') || $Element.attr('id');
		if (!Key || typeof SimpleCallbackModal === 'undefined') {
			return;
		}

		var Current = new URL(window.location.href).searchParams.get(Key) || '';
		SimpleCallbackModal.Prompt({
			title: $Element.attr('data-query-title') || 'Set Page Option',
			label: $Element.attr('data-query-label') || Key,
			value: Current,
			confirmLabel: 'Apply',
			onConfirm: function (Value) {
				var Url = new URL(window.location.href);
				if (Value) {
					Url.searchParams.set(Key, Value);
				} else {
					Url.searchParams.delete(Key);
				}
				window.location.href = Url.toString();
			}
		});
	});
});


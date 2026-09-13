/* Shared text-only tooltips for compact controls. */
jQuery(function ($) {
	'use strict';

	var $Tooltip = $('<div class="simple-tooltip" role="tooltip"></div>').hide().appendTo('body');

	$(document)
		.on('mouseenter', '[data-tooltip]', function () {
			var Text = $(this).attr('data-tooltip') || '';
			if (Text) {
				$Tooltip.text(Text).show();
			}
		})
		.on('mousemove', '[data-tooltip]', function (Event) {
			var Left = Event.clientX + 12;
			var Top = Event.clientY + 12;
			Left = Math.min(Left, window.innerWidth - $Tooltip.outerWidth() - 8);
			Top = Math.min(Top, window.innerHeight - $Tooltip.outerHeight() - 8);
			$Tooltip.css({ left: Math.max(8, Left), top: Math.max(8, Top) });
		})
		.on('mouseleave', '[data-tooltip]', function () {
			$Tooltip.hide().text('');
		});
});


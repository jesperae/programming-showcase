/*
 * Simple Callback Modal
 * Shared alert, confirmation, prompt, and choice dialogs.
 */
var SimpleCallbackModal = (function () {
	'use strict';

	var $Overlay;
	var $Modal;
	var ConfirmHandler;
	var CancelHandler;

	function BuildModal() {
		if ($Modal && $Modal.length) {
			return;
		}

		$Overlay = jQuery('<div class="simple-modal-overlay"></div>').hide();
		$Modal = jQuery(
			'<div class="simple-modal" role="dialog" aria-modal="true">'
				+ '<div class="simple-modal-title"></div>'
				+ '<div class="simple-modal-content"></div>'
				+ '<div class="simple-modal-actions">'
					+ '<button type="button" data-modal-action="cancel">Cancel</button>'
					+ '<button type="button" class="primary" data-modal-action="confirm">Continue</button>'
				+ '</div>'
			+ '</div>'
		).hide();

		jQuery('body').append($Overlay, $Modal);
		$Overlay.on('click', function () {
			Close(false);
		});
		$Modal.on('click', '[data-modal-action]', function () {
			Close(jQuery(this).data('modal-action') === 'confirm');
		});
		jQuery(document).on('keydown.simpleModal', function (Event) {
			if (!$Modal.is(':visible')) {
				return;
			}
			if (Event.key === 'Escape') {
				Close(false);
			}
			if (Event.key === 'Enter' && !jQuery(Event.target).is('textarea')) {
				Close(true);
			}
		});
	}

	function Open(Options) {
		BuildModal();
		Options = Options || {};
		ConfirmHandler = Options.onConfirm || null;
		CancelHandler = Options.onCancel || null;

		$Modal.find('.simple-modal-title').text(Options.title || 'Confirm');
		$Modal.find('.simple-modal-content').empty().append(Options.content || '');
		$Modal.find('[data-modal-action="confirm"]').text(Options.confirmLabel || 'Continue');
		$Modal.find('[data-modal-action="cancel"]').text(Options.cancelLabel || 'Cancel').toggle(Options.showCancel !== false);
		$Overlay.show();
		$Modal.show().find('input, textarea, select, button').filter(':visible').first().trigger('focus');
	}

	function Close(Confirmed) {
		if (!$Modal || !$Modal.is(':visible')) {
			return;
		}

		var Values = {};
		$Modal.find('[name]').each(function () {
			Values[this.name] = jQuery(this).val();
		});
		$Modal.hide();
		$Overlay.hide();

		var Handler = Confirmed ? ConfirmHandler : CancelHandler;
		ConfirmHandler = null;
		CancelHandler = null;
		if (Handler) {
			Handler(Values);
		}
	}

	function Alert(Message, Title, OnClose) {
		Open({
			title: Title || 'Notice',
			content: jQuery('<div></div>').text(Message || ''),
			confirmLabel: 'OK',
			showCancel: false,
			onConfirm: OnClose
		});
	}

	function Confirm(Message, Title, OnYes, OnNo) {
		Open({
			title: Title || 'Confirm',
			content: jQuery('<div></div>').text(Message || ''),
			confirmLabel: 'Yes',
			cancelLabel: 'No',
			onConfirm: OnYes,
			onCancel: OnNo
		});
	}

	function Prompt(Options) {
		Options = Options || {};
		var Name = Options.name || 'value';
		var $Content = jQuery('<div class="modal-field"></div>');
		$Content.append(jQuery('<label></label>').attr('for', 'ModalPromptValue').text(Options.label || 'Value'));
		$Content.append(jQuery(Options.multiline ? '<textarea></textarea>' : '<input type="text">')
			.attr({ id: 'ModalPromptValue', name: Name })
			.val(Options.value || ''));
		Open({
			title: Options.title || 'Enter Value',
			content: $Content,
			confirmLabel: Options.confirmLabel || 'Save',
			onConfirm: function (Values) {
				if (Options.onConfirm) {
					Options.onConfirm(Values[Name] || '');
				}
			}
		});
	}

	function Choose(Options) {
		Options = Options || {};
		var Name = Options.name || 'choice';
		var $Content = jQuery('<div class="modal-field"></div>');
		var $Select = jQuery('<select></select>').attr('name', Name);
		jQuery.each(Options.options || [], function (_, Choice) {
			var Value = Choice.value !== undefined ? Choice.value : Choice;
			var Label = Choice.label !== undefined ? Choice.label : Choice;
			$Select.append(jQuery('<option></option>').val(Value).text(Label));
		});
		$Content.append(jQuery('<label></label>').text(Options.label || 'Choose an option'), $Select);
		Open({
			title: Options.title || 'Choose',
			content: $Content,
			onConfirm: function (Values) {
				if (Options.onConfirm) {
					Options.onConfirm(Values[Name] || '');
				}
			}
		});
	}

	function BuildStatusMessage(Message, IsSuccess, Duration) {
		var $Toast = jQuery('<div class="simple-toast"></div>')
			.addClass(IsSuccess ? 'ok' : 'error')
			.text(Message || '')
			.appendTo('body');
		setTimeout(function () {
			$Toast.fadeOut(200, function () { $Toast.remove(); });
		}, Duration || (IsSuccess ? 3500 : 6500));
	}

	return {
		Alert: Alert,
		Confirm: Confirm,
		Prompt: Prompt,
		Choose: Choose,
		Close: Close,
		BuildStatusMessage: BuildStatusMessage
	};
})();


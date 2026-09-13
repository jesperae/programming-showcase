/*
 * Simple AJAX Request
 * Chainable client for action-based JSON endpoints.
 */
(function (global) {
	'use strict';

	var AjaxStatus = {
		GetMessageElement: function () {
			return jQuery('#AjaxStatusMessage');
		},
		GetLoadingElement: function () {
			return jQuery('#AjaxStatusLoading');
		},
		SetMessage: function (Text, IsError) {
			var $Message = this.GetMessageElement();
			var $Loading = this.GetLoadingElement();
			$Message.removeClass('error ok').text(Text || '');
			if (Text) {
				$Message.addClass(IsError ? 'error' : 'ok');
			}
			$Loading.hide();
		},
		SetLoading: function (Text) {
			this.GetLoadingElement().show();
			this.GetMessageElement().removeClass('error ok').text(Text || 'Loading...');
		},
		Clear: function () {
			this.SetMessage('', false);
		}
	};

	function AjaxRequester(Url) {
		this.Url = Url;
	}

	AjaxRequester.prototype.call = function (Action, Data) {
		return new AjaxCall(this.Url, Action, Data || {});
	};

	function AjaxCall(Url, Action, Data) {
		this.Url = Url;
		this.Action = Action;
		this.Data = Data;
		this.CompleteHandler = null;
		this.ErrorHandler = null;
		this.FinallyHandler = null;
		this.PendingText = 'Loading...';
	}

	AjaxCall.prototype.onComplete = function (Handler) {
		this.CompleteHandler = Handler;
		return this;
	};

	AjaxCall.prototype.onError = function (Handler) {
		this.ErrorHandler = Handler;
		return this;
	};

	AjaxCall.prototype.onFinally = function (Handler) {
		this.FinallyHandler = Handler;
		return this;
	};

	AjaxCall.prototype.pendingMessage = function (Text) {
		this.PendingText = Text;
		return this;
	};

	AjaxCall.prototype.send = function () {
		var Self = this;
		AjaxStatus.SetLoading(this.PendingText);

		return jQuery.ajax({
			url: this.Url,
			type: 'POST',
			contentType: 'application/json',
			dataType: 'json',
			data: JSON.stringify({ action: this.Action, data: this.Data })
		}).done(function (Response) {
			if (!Response || typeof Response.ok === 'undefined') {
				Self.Fail('Invalid response format.', {});
				return;
			}

			if (!Response.ok) {
				Self.Fail(Response.message || 'Request failed.', Response);
				return;
			}

			AjaxStatus.SetMessage(Response.message || '', false);
			if (Self.CompleteHandler) {
				Self.CompleteHandler(Response.result || {}, Response);
			}
		}).fail(function (Request) {
			var Response = Request.responseJSON || {};
			Self.Fail(Response.message || 'Request failed.', Response);
		}).always(function () {
			if (Self.FinallyHandler) {
				Self.FinallyHandler();
			}
		});
	};

	AjaxCall.prototype.Fail = function (Message, Response) {
		AjaxStatus.SetMessage(Message, true);
		if (this.ErrorHandler) {
			this.ErrorHandler(Message, Response || {});
		}
	};

	global.AjaxStatus = AjaxStatus;
	global.AjaxRequester = AjaxRequester;
})(window);


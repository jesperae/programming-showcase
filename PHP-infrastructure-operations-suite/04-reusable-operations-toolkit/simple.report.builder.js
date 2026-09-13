/*
 * Simple Report Builder
 * One report definition for print and CSV output.
 */
(function (global) {
	'use strict';

	function EscapeHtml(Value) {
		return jQuery('<div></div>').text(Value === null || Value === undefined ? '' : Value).html();
	}

	function EscapeCsv(Value) {
		var Text = String(Value === null || Value === undefined ? '' : Value);
		return '"' + Text.replace(/"/g, '""') + '"';
	}

	function GetColumnValue(Column, Row) {
		var Value = Row[Column.key];
		return Column.format ? Column.format(Value, Row) : Value;
	}

	function BuildHtml(Config) {
		Config = Config || {};
		var Html = '<article class="print-report">';
		Html += '<h1>' + EscapeHtml(Config.title || 'Report') + '</h1>';
		if (Config.subtitle) {
			Html += '<p class="report-subtitle">' + EscapeHtml(Config.subtitle) + '</p>';
		}
		if (Config.meta && Config.meta.length) {
			Html += '<dl class="report-meta">';
			jQuery.each(Config.meta, function (_, Item) {
				Html += '<dt>' + EscapeHtml(Item.label) + '</dt><dd>' + EscapeHtml(Item.value) + '</dd>';
			});
			Html += '</dl>';
		}
		Html += '<table><thead><tr>';
		jQuery.each(Config.columns || [], function (_, Column) {
			Html += '<th>' + EscapeHtml(Column.label || Column.key) + '</th>';
		});
		Html += '</tr></thead><tbody>';
		if (!(Config.rows || []).length) {
			Html += '<tr><td colspan="' + (Config.columns || []).length + '">No records.</td></tr>';
		}
		jQuery.each(Config.rows || [], function (_, Row) {
			Html += '<tr>';
			jQuery.each(Config.columns || [], function (_, Column) {
				Html += '<td>' + EscapeHtml(GetColumnValue(Column, Row)) + '</td>';
			});
			Html += '</tr>';
		});
		Html += '</tbody></table></article>';
		return Html;
	}

	function BuildCsv(Config) {
		var Lines = [];
		jQuery.each(Config.meta || [], function (_, Item) {
			Lines.push(EscapeCsv(Item.label) + ',' + EscapeCsv(Item.value));
		});
		if ((Config.meta || []).length) {
			Lines.push('');
		}
		Lines.push((Config.columns || []).map(function (Column) {
			return EscapeCsv(Column.label || Column.key);
		}).join(','));
		jQuery.each(Config.rows || [], function (_, Row) {
			Lines.push((Config.columns || []).map(function (Column) {
				return EscapeCsv(GetColumnValue(Column, Row));
			}).join(','));
		});
		return Lines.join('\r\n');
	}

	function Print(Config) {
		var Window = global.open('', '_blank');
		if (!Window) {
			return false;
		}
		Window.document.write('<!doctype html><html><head><title>' + EscapeHtml(Config.title || 'Report') + '</title>'
			+ '<style>body{font:12px Arial;margin:24px;color:#17222d}h1{margin:0 0 4px}.report-subtitle{color:#536477}.report-meta{display:grid;grid-template-columns:140px 1fr;max-width:700px}.report-meta dt{font-weight:bold}.report-meta dd{margin:0}table{border-collapse:collapse;width:100%;margin-top:18px}th,td{border:1px solid #9ba8b5;padding:6px;text-align:left}th{background:#eaf2f8}@media print{body{margin:10mm}}</style>'
			+ '</head><body>' + BuildHtml(Config) + '</body></html>');
		Window.document.close();
		Window.focus();
		setTimeout(function () { Window.print(); }, 150);
		return true;
	}

	function DownloadCsv(Config, Filename) {
		var BlobValue = new Blob([BuildCsv(Config)], { type: 'text/csv;charset=utf-8' });
		var Url = global.URL.createObjectURL(BlobValue);
		var Link = document.createElement('a');
		Link.href = Url;
		Link.download = Filename || 'report.csv';
		document.body.appendChild(Link);
		Link.click();
		document.body.removeChild(Link);
		global.URL.revokeObjectURL(Url);
	}

	global.SimpleReportBuilder = {
		BuildHtml: BuildHtml,
		BuildCsv: BuildCsv,
		Print: Print,
		DownloadCsv: DownloadCsv
	};
})(window);

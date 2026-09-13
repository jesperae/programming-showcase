<?php
require_once(__DIR__ . '/../04-reusable-operations-toolkit/simple.ajax.endpoint.inc.php');
require_once(__DIR__ . '/../04-reusable-operations-toolkit/demo.asset.repository.inc.php');

function BuildSiteOptions()
{
	$Html = '';
	foreach (DemoAssetRepository::GetSites() as $Idx => $Site) {
		$Selected = $Idx === 0 ? ' selected' : '';
		$Html .= "<option value='" . EncodeHtml($Site['VALUE']) . "'$Selected>" . EncodeHtml($Site['LABEL']) . "</option>";
	}
	return $Html;
}

function BuildTypeOptions()
{
	$Labels = ['ENDPOINT' => 'Endpoints', 'NETWORK_APPLIANCE' => 'Network Appliances', 'TEST_DEVICE' => 'Test Devices'];
	$Html = '';
	foreach (DemoAssetRepository::GetTypes() as $Idx => $Type) {
		$Checked = $Idx < 2 ? ' checked' : '';
		$Html .= "<label><input type='checkbox' class='AssetType' value='" . EncodeHtml($Type) . "'$Checked> " . EncodeHtml($Labels[$Type]) . "</label>";
	}
	return $Html;
}
?>
<!doctype html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title>Asset Audit and Reconciliation</title>
	<link rel="stylesheet" href="../04-reusable-operations-toolkit/simple.styles.css">
	<script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.ajax.request.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.callback.modal.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.report.builder.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.tooltip.js"></script>
</head>
<body>
	<header class="app-header">
		<div class="app-mark">AR</div>
		<div>
			<h1>Asset Audit and Reconciliation</h1>
			<div class="app-subtitle">Compare expected inventory with physical scans</div>
		</div>
		<nav class="app-nav"><a href="../">Suite Home</a></nav>
	</header>
	<main class="page-shell">
		<section class="panel">
			<div class="panel-title">Audit Controls</div>
			<div class="panel-content">
				<div class="controls">
					<div class="field">
						<label for="SiteCode">Site</label>
						<select id="SiteCode"><?php echo BuildSiteOptions(); ?></select>
					</div>
					<div class="field grow">
						<label>Asset Types</label>
						<div class="type-options"><?php echo BuildTypeOptions(); ?></div>
					</div>
					<div class="field">
						<label for="AuditSelect">Audit History</label>
						<select id="AuditSelect"><option value="">Current selection</option></select>
					</div>
					<button type="button" id="StartAudit" class="primary" data-tooltip="Capture the current expected inventory.">Start Audit</button>
					<button type="button" id="CompleteAudit" data-tooltip="Lock the audit with completion notes.">Complete</button>
					<button type="button" id="ReAudit" data-tooltip="Refresh inventory and reuse prior scan identifiers.">Repeat Audit</button>
					<button type="button" id="PrintAudit">Print / Save PDF</button>
					<button type="button" id="ExportAudit">Download CSV</button>
				</div>
				<?php echo BuildAjaxStatusMessage(); ?>
			</div>
		</section>

		<section class="panel" id="AuditSummaryPanel" style="display:none">
			<div class="panel-title">Audit Summary</div>
			<div class="panel-content">
				<div id="AuditSummary" class="summary-grid"></div>
			</div>
		</section>

		<div class="two-column">
			<section class="panel">
				<div class="panel-title">Expected at Site <span class="count-badge" id="ExpectedCount">0</span></div>
				<div class="panel-content table-wrap">
					<table class="table">
						<thead><tr><th>Asset ID</th><th>Serial</th><th>Model</th><th>Type</th><th>Status</th></tr></thead>
						<tbody id="ExpectedBody"><tr class="empty-row"><td colspan="5">Select a site to load inventory.</td></tr></tbody>
					</table>
				</div>
			</section>

			<section class="panel">
				<div class="panel-title">Scanned or Entered <span class="count-badge" id="ScannedCount">0</span></div>
				<div class="panel-content table-wrap">
					<table class="table">
						<thead><tr><th>Asset ID</th><th>Serial</th><th>Site</th><th>Status</th><th>Scanned By</th><th></th></tr></thead>
						<tbody id="ScannedBody"><tr class="empty-row"><td colspan="6">Start an audit to record scans.</td></tr></tbody>
					</table>
				</div>
			</section>
		</div>

		<section class="panel">
			<div class="panel-title">Scan Workflow</div>
			<div class="panel-content">
				<div class="scan-row">
					<div class="field grow">
						<label for="ScanInput">Asset ID, Serial, or Batch Code</label>
						<input type="text" id="ScanInput" autocomplete="off" placeholder="Example: DEMO-0001 or a completed batch code">
					</div>
					<button type="button" id="AddScan" class="primary">Record Scan</button>
					<button type="button" id="MassInput">Mass Input</button>
					<button type="button" id="ClearScans" class="danger">Clear Scans</button>
				</div>
				<p class="help-text">Enter 4–7 hexadecimal characters to resolve a partial asset ID. A two-character code imports a completed asset batch.</p>
			</div>
		</section>
	</main>

	<script>
		jQuery(function ($) {
			var Ajax = new AjaxRequester('api.php');
			var ViewState = { meta: null, expected: [], scanned: [], summary: {}, audits: [] };

			function EscapeHtml(Value) {
				return $('<div></div>').text(Value === null || Value === undefined ? '' : Value).html();
			}

			function GetTypes() {
				return $('.AssetType:checked').map(function () { return this.value; }).get();
			}

			function Request(Action, Data, OnComplete, PendingText) {
				return Ajax.call(Action, Data || {})
					.pendingMessage(PendingText || 'Working...')
					.onComplete(OnComplete)
					.send();
			}

			function LoadContext() {
				var Types = GetTypes();
				if (!Types.length) {
					$('.AssetType').first().prop('checked', true);
					Types = GetTypes();
				}
				Request('GET_CONTEXT', { site_code: $('#SiteCode').val(), types: Types }, function (Result) {
					ViewState.audits = Result.audits || [];
					BuildAuditChoices();
					if (Result.active && Result.active.META) {
						ApplyState(Result.active);
					} else {
						ApplyState({
							META: null,
							EXPECTED: (Result.current_inventory || []).map(function (Asset) {
								Asset.RECONCILIATION_STATUS = 'EXPECTED';
								return Asset;
							}),
							SCANNED: [],
							SUMMARY: {}
						});
					}
				}, 'Loading site inventory...');
			}

			function ApplyState(State) {
				ViewState.meta = State.META || null;
				ViewState.expected = State.EXPECTED || [];
				ViewState.scanned = State.SCANNED || [];
				ViewState.summary = State.SUMMARY || {};
				RenderAll();
			}

			function BuildAuditChoices() {
				var $Select = $('#AuditSelect').empty().append('<option value="">Current selection</option>');
				$.each(ViewState.audits, function (_, Audit) {
					var Label = 'Audit ' + Audit.AUDIT_ID + ' | ' + Audit.DOCUMENT_STATUS + ' | ' + Audit.CREATED_DATE;
					$Select.append($('<option></option>').val(Audit.AUDIT_ID).text(Label));
				});
			}

			function RenderAll() {
				RenderSummary();
				RenderExpected();
				RenderScanned();
				SetControlState();
			}

			function RenderSummary() {
				var Meta = ViewState.meta;
				$('#AuditSummaryPanel').toggle(!!Meta);
				if (!Meta) {
					return;
				}
				var Summary = ViewState.summary;
				var Values = [
					['Audit', Meta.AUDIT_ID],
					['Status', Meta.DOCUMENT_STATUS],
					['Started', Meta.CREATED_BY + ' | ' + Meta.CREATED_DATE],
					['Completed', Meta.COMPLETED_DATE ? Meta.COMPLETED_BY + ' | ' + Meta.COMPLETED_DATE : 'In progress'],
					['Expected', Summary.EXPECTED || 0],
					['Present', Summary.PRESENT || 0],
					['Missing', Summary.MISSING || 0],
					['Problems', (Summary.ELSEWHERE || 0) + (Summary.NOT_FOUND || 0) + (Summary.PROBLEM || 0)],
					['Notes', Meta.COMPLETION_NOTES || '']
				];
				$('#AuditSummary').html(Values.map(function (Item) {
					return '<div><strong>' + EscapeHtml(Item[0]) + ':</strong> ' + EscapeHtml(Item[1]) + '</div>';
				}).join(''));
			}

			function StatusClass(Status) {
				return 'status-' + String(Status || '').toLowerCase().replace(/[^a-z]+/g, '-');
			}

			function RenderExpected() {
				$('#ExpectedCount').text(ViewState.expected.length);
				if (!ViewState.expected.length) {
					$('#ExpectedBody').html('<tr class="empty-row"><td colspan="5">No matching inventory.</td></tr>');
					return;
				}
				$('#ExpectedBody').html(ViewState.expected.map(function (Asset) {
					var Status = Asset.RECONCILIATION_STATUS || 'EXPECTED';
					return '<tr><td>' + EscapeHtml(Asset.ASSET_ID) + '</td><td>' + EscapeHtml(Asset.SERIAL) + '</td><td>'
						+ EscapeHtml(Asset.MODEL) + '</td><td>' + EscapeHtml(Asset.TYPE) + '</td><td><span class="status-badge '
						+ StatusClass(Status) + '">' + EscapeHtml(Status) + '</span></td></tr>';
				}).join(''));
			}

			function RenderScanned() {
				$('#ScannedCount').text(ViewState.scanned.length);
				if (!ViewState.scanned.length) {
					$('#ScannedBody').html('<tr class="empty-row"><td colspan="6">No scans recorded.</td></tr>');
					return;
				}
				$('#ScannedBody').html(ViewState.scanned.map(function (Scan) {
					var Detail = Scan.STATUS_DETAIL ? ' (' + Scan.STATUS_DETAIL + ')' : '';
					return '<tr><td>' + EscapeHtml(Scan.ASSET_ID) + '</td><td>' + EscapeHtml(Scan.SERIAL) + '</td><td>'
						+ EscapeHtml(Scan.SITE_CODE) + '</td><td><span class="status-badge ' + StatusClass(Scan.STATUS) + '">'
						+ EscapeHtml(Scan.STATUS + Detail) + '</span></td><td>' + EscapeHtml(Scan.SCANNED_BY) + '<br><span class="help-text">'
						+ EscapeHtml(Scan.SCANNED_DATE) + '</span></td><td><button type="button" class="RemoveScan danger" data-key="'
						+ EscapeHtml(Scan.SCAN_KEY) + '">Remove</button></td></tr>';
				}).join(''));
			}

			function SetControlState() {
				var HasAudit = !!ViewState.meta;
				var Completed = HasAudit && ViewState.meta.COMPLETED === 'Y';
				$('#StartAudit').prop('disabled', HasAudit && !Completed);
				$('#CompleteAudit').prop('disabled', !HasAudit || Completed);
				$('#ReAudit').prop('disabled', !HasAudit || !Completed);
				$('#ScanInput, #AddScan, #MassInput, #ClearScans').prop('disabled', !HasAudit || Completed);
				$('.RemoveScan').prop('disabled', !HasAudit || Completed);
				$('#PrintAudit, #ExportAudit').prop('disabled', !HasAudit);
			}

			function RecordInput(Value) {
				Value = $.trim(Value || '');
				if (!Value || !ViewState.meta) {
					return;
				}
				if (/^[A-Za-z0-9]{2}$/.test(Value)) {
					ImportBatch(Value);
					return;
				}
				if (/^[0-9a-fA-F]{4,7}$/.test(Value)) {
					ResolveSuffix(Value);
					return;
				}
				Request('CHECK_ASSET', { audit_id: ViewState.meta.AUDIT_ID, input: Value }, function (Result) {
					ApplyState(Result.state);
					$('#ScanInput').val('').trigger('focus');
				}, 'Checking asset...');
			}

			function ResolveSuffix(Suffix) {
				Request('LOOKUP_SUFFIX', { site_code: $('#SiteCode').val(), types: GetTypes(), suffix: Suffix }, function (Result) {
					var Choices = Result.choices || [];
					if (!Choices.length) {
						RecordInput('UNKNOWN-' + Suffix);
					} else if (Choices.length === 1) {
						RecordInput(Choices[0].value);
					} else {
						SimpleCallbackModal.Choose({
							title: 'Choose Matching Asset',
							label: 'Several asset IDs share that suffix.',
							options: Choices,
							onConfirm: RecordInput
						});
					}
				}, 'Resolving partial asset ID...');
			}

			function ImportBatch(BatchCode) {
				Request('IMPORT_BATCH', { batch_code: BatchCode }, function (Result) {
					Request('MASS_CHECK', { audit_id: ViewState.meta.AUDIT_ID, inputs: Result.inputs || [] }, function (BatchResult) {
						ApplyState(BatchResult.state);
					}, 'Importing batch assets...');
				}, 'Validating asset batch...');
			}

			function BuildReport() {
				var Meta = ViewState.meta || {};
				var Rows = ViewState.expected.map(function (Asset) {
					return {
						asset_id: Asset.ASSET_ID,
						serial: Asset.SERIAL,
						model: Asset.MODEL,
						type: Asset.TYPE,
						status: Asset.RECONCILIATION_STATUS
					};
				});
				$.each(ViewState.scanned, function (_, Scan) {
					if (Scan.STATUS !== 'PRESENT') {
						Rows.push({ asset_id: Scan.ASSET_ID, serial: Scan.SERIAL, model: Scan.MODEL, type: Scan.TYPE, status: Scan.STATUS });
					}
				});
				return {
					title: 'Asset Reconciliation Audit ' + (Meta.AUDIT_ID || ''),
					subtitle: Meta.DOCUMENT_STATUS || '',
					meta: [
						{ label: 'Site', value: Meta.SITE_CODE || '' },
						{ label: 'Types', value: Meta.TYPES || '' },
						{ label: 'Started', value: (Meta.CREATED_BY || '') + ' | ' + (Meta.CREATED_DATE || '') },
						{ label: 'Completed', value: (Meta.COMPLETED_BY || '') + ' | ' + (Meta.COMPLETED_DATE || '') },
						{ label: 'Notes', value: Meta.COMPLETION_NOTES || '' }
					],
					columns: [
						{ key: 'asset_id', label: 'Asset ID' },
						{ key: 'serial', label: 'Serial' },
						{ key: 'model', label: 'Model' },
						{ key: 'type', label: 'Type' },
						{ key: 'status', label: 'Status' }
					],
					rows: Rows
				};
			}

			$('#SiteCode, .AssetType').on('change', LoadContext);
			$('#AuditSelect').on('change', function () {
				if (!this.value) {
					LoadContext();
					return;
				}
				Request('LOAD_AUDIT', { audit_id: this.value }, ApplyState, 'Loading audit...');
			});
			$('#StartAudit').on('click', function () {
				Request('START_AUDIT', { site_code: $('#SiteCode').val(), types: GetTypes() }, function (State) {
					ApplyState(State);
					LoadContext();
				}, 'Starting audit...');
			});
			$('#AddScan').on('click', function () { RecordInput($('#ScanInput').val()); });
			$('#ScanInput').on('keydown', function (Event) {
				if (Event.key === 'Enter') {
					Event.preventDefault();
					RecordInput(this.value);
				}
			});
			$('#MassInput').on('click', function () {
				SimpleCallbackModal.Prompt({
					title: 'Mass Asset Input',
					label: 'Enter one asset ID or serial per line.',
					multiline: true,
					confirmLabel: 'Process',
					onConfirm: function (Value) {
						Request('MASS_CHECK', { audit_id: ViewState.meta.AUDIT_ID, inputs: Value }, function (Result) {
							ApplyState(Result.state);
						}, 'Processing asset list...');
					}
				});
			});
			$('#CompleteAudit').on('click', function () {
				SimpleCallbackModal.Prompt({
					title: 'Complete Audit',
					label: 'Required completion notes',
					multiline: true,
					confirmLabel: 'Complete',
					onConfirm: function (Notes) {
						Request('COMPLETE_AUDIT', { audit_id: ViewState.meta.AUDIT_ID, notes: Notes }, ApplyState, 'Completing audit...');
					}
				});
			});
			$('#ReAudit').on('click', function () {
				SimpleCallbackModal.Confirm('Create a new audit using current inventory and these scan identifiers?', 'Repeat Audit', function () {
					Request('RE_AUDIT', { audit_id: ViewState.meta.AUDIT_ID }, ApplyState, 'Creating repeat audit...');
				});
			});
			$('#ClearScans').on('click', function () {
				SimpleCallbackModal.Confirm('Remove every scan from this audit?', 'Clear Scans', function () {
					Request('CLEAR_SCANS', { audit_id: ViewState.meta.AUDIT_ID }, ApplyState, 'Clearing scans...');
				});
			});
			$('#ScannedBody').on('click', '.RemoveScan', function () {
				Request('REMOVE_SCAN', { audit_id: ViewState.meta.AUDIT_ID, scan_key: $(this).data('key') }, ApplyState, 'Removing scan...');
			});
			$('#PrintAudit').on('click', function () { SimpleReportBuilder.Print(BuildReport()); });
			$('#ExportAudit').on('click', function () {
				SimpleReportBuilder.DownloadCsv(BuildReport(), 'asset-audit-' + ViewState.meta.AUDIT_ID + '.csv');
			});

			LoadContext();
		});
	</script>
</body>
</html>

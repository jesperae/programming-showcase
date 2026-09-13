<?php
require_once(__DIR__ . '/../04-reusable-operations-toolkit/simple.ajax.endpoint.inc.php');
?>
<!doctype html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title>Asset Batch and Transfer Manager</title>
	<link rel="stylesheet" href="../04-reusable-operations-toolkit/simple.styles.css">
	<script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.ajax.request.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.callback.modal.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.search.dropdown.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.report.builder.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.tooltip.js"></script>
</head>
<body>
	<header class="app-header">
		<div class="app-mark">BM</div>
		<div>
			<h1>Asset Batch and Transfer Manager</h1>
			<div class="app-subtitle">Build validated equipment lists for downstream work</div>
		</div>
		<nav class="app-nav"><a href="../">Suite Home</a></nav>
	</header>
	<main class="page-shell">
		<section class="panel">
			<div class="panel-title">Batch Controls</div>
			<div class="panel-content">
				<div class="controls">
					<div class="field search-control">
						<label for="BatchCode">Batch Code</label>
						<input type="text" id="BatchCode" maxlength="2" autocomplete="off" placeholder="Example: AA">
						<div id="BatchDropdown" class="search-dropdown" style="display:none"></div>
					</div>
					<button type="button" id="LoadBatch">Load Batch</button>
					<button type="button" id="CreateBatch" class="primary" data-tooltip="Create the next available short batch code.">Create New Batch</button>
					<button type="button" id="CompleteBatch" data-tooltip="Lock this batch for downstream use.">Complete</button>
					<button type="button" id="CopyBatch" data-tooltip="Copy a completed batch into a new revision.">Copy Batch</button>
					<button type="button" id="PrintBatch">Print / Save PDF</button>
					<button type="button" id="ExportBatch">Download CSV</button>
				</div>
				<?php echo BuildAjaxStatusMessage(); ?>
			</div>
		</section>

		<section class="panel" id="BatchSummaryPanel" style="display:none">
			<div class="panel-title">Batch Summary</div>
			<div class="panel-content"><div id="BatchSummary" class="summary-grid"></div></div>
		</section>

		<section class="panel">
			<div class="panel-title">Batch Assets <span class="count-badge" id="AssetCount">0</span></div>
			<div class="panel-content table-wrap">
				<table class="table">
					<thead><tr><th>Asset ID</th><th>Serial</th><th>Model</th><th>Type</th><th>Current Site</th><th>Added By</th><th></th></tr></thead>
					<tbody id="AssetBody"><tr class="empty-row"><td colspan="7">Create or load a batch.</td></tr></tbody>
				</table>
			</div>
		</section>

		<section class="panel">
			<div class="panel-title">Add Assets</div>
			<div class="panel-content">
				<div class="scan-row">
					<div class="field grow">
						<label for="AssetInput">Asset ID, Serial, or Completed Batch Code</label>
						<input type="text" id="AssetInput" autocomplete="off" placeholder="Example: DEMO-0001">
					</div>
					<button type="button" id="AddAsset" class="primary">Add Asset</button>
					<button type="button" id="MassInput">Mass Input</button>
				</div>
				<p class="help-text">Enter 4–7 hexadecimal characters to resolve a partial asset ID. Enter another completed batch code to import its assets.</p>
			</div>
		</section>
	</main>

	<script>
		jQuery(function ($) {
			var Ajax = new AjaxRequester('api.php');
			var CurrentBatch = null;
			var CurrentAssets = [];
			var BatchChoices = [];
			var BatchDropdown = new SimpleSearchDropdown({
				input: '#BatchCode',
				menu: '#BatchDropdown',
				items: [],
				onSelect: function () { LoadBatch(); }
			});

			function EscapeHtml(Value) {
				return $('<div></div>').text(Value === null || Value === undefined ? '' : Value).html();
			}

			function NormalizeCode(Value) {
				return String(Value || '').toUpperCase().replace(/[^A-Z0-9]/g, '').substring(0, 2);
			}

			function Request(Action, Data, OnComplete, PendingText) {
				return Ajax.call(Action, Data || {})
					.pendingMessage(PendingText || 'Working...')
					.onComplete(OnComplete)
					.send();
			}

			function RefreshChoices() {
				Request('GET_BATCH_CHOICES', {}, function (Result) {
					BatchChoices = Result.choices || [];
					BatchDropdown.SetItems(BatchChoices);
				}, 'Loading batches...');
			}

			function LoadBatch(Code) {
				Code = NormalizeCode(Code || $('#BatchCode').val());
				if (!Code) {
					return;
				}
				Request('LOAD_BATCH', { batch_code: Code }, ApplyResult, 'Loading batch ' + Code + '...');
			}

			function ApplyResult(Result) {
				CurrentBatch = Result.BATCH || null;
				CurrentAssets = Result.ITEMS || [];
				if (CurrentBatch) {
					$('#BatchCode').val(CurrentBatch.BATCH_CODE);
				}
				RenderAll();
				RefreshChoices();
			}

			function RenderAll() {
				RenderSummary();
				RenderAssets();
				SetControlState();
			}

			function RenderSummary() {
				$('#BatchSummaryPanel').toggle(!!CurrentBatch);
				if (!CurrentBatch) {
					return;
				}
				var Values = [
					['Batch', CurrentBatch.BATCH_CODE],
					['Status', CurrentBatch.STATUS],
					['Assets', CurrentBatch.ITEM_COUNT],
					['Created', CurrentBatch.CREATED_BY + ' | ' + CurrentBatch.CREATED_DATE],
					['Updated', CurrentBatch.UPDATED_BY + ' | ' + CurrentBatch.UPDATED_DATE],
					['Completed', CurrentBatch.COMPLETED_DATE ? CurrentBatch.COMPLETED_BY + ' | ' + CurrentBatch.COMPLETED_DATE : 'In progress'],
					['Copied From', CurrentBatch.COPIED_FROM || ''],
					['Notes', CurrentBatch.NOTES || '']
				];
				$('#BatchSummary').html(Values.map(function (Item) {
					return '<div><strong>' + EscapeHtml(Item[0]) + ':</strong> ' + EscapeHtml(Item[1]) + '</div>';
				}).join(''));
			}

			function RenderAssets() {
				$('#AssetCount').text(CurrentAssets.length);
				if (!CurrentAssets.length) {
					$('#AssetBody').html('<tr class="empty-row"><td colspan="7">No assets in this batch.</td></tr>');
					return;
				}
				$('#AssetBody').html(CurrentAssets.map(function (Asset) {
					return '<tr><td>' + EscapeHtml(Asset.ASSET_ID) + '</td><td>' + EscapeHtml(Asset.SERIAL) + '</td><td>'
						+ EscapeHtml(Asset.MODEL) + '</td><td>' + EscapeHtml(Asset.TYPE) + '</td><td>' + EscapeHtml(Asset.SITE_CODE)
						+ '</td><td>' + EscapeHtml(Asset.ADDED_BY) + '<br><span class="help-text">' + EscapeHtml(Asset.ADDED_DATE)
						+ '</span></td><td><button type="button" class="RemoveAsset danger" data-id="' + EscapeHtml(Asset.ASSET_ID)
						+ '">Remove</button></td></tr>';
				}).join(''));
			}

			function SetControlState() {
				var HasBatch = !!CurrentBatch;
				var Completed = HasBatch && parseInt(CurrentBatch.COMPLETED, 10) === 1;
				var CanEdit = HasBatch && parseInt(CurrentBatch.CAN_EDIT, 10) === 1;
				$('#CompleteBatch').prop('disabled', !HasBatch || Completed || !CanEdit || !CurrentAssets.length);
				$('#CopyBatch').prop('disabled', !HasBatch || !Completed);
				$('#PrintBatch, #ExportBatch').prop('disabled', !HasBatch);
				$('#AssetInput, #AddAsset, #MassInput').prop('disabled', !HasBatch || Completed || !CanEdit);
				$('.RemoveAsset').prop('disabled', !HasBatch || Completed || !CanEdit);
			}

			function SubmitInput(Value) {
				Value = $.trim(Value || '');
				if (!CurrentBatch || !Value) {
					return;
				}
				if (/^[A-Za-z0-9]{2}$/.test(Value) && NormalizeCode(Value) !== CurrentBatch.BATCH_CODE) {
					ImportBatch(Value);
					return;
				}
				if (/^[0-9a-fA-F]{4,7}$/.test(Value)) {
					ResolveSuffix(Value);
					return;
				}
				Request('ADD_ASSET', { batch_code: CurrentBatch.BATCH_CODE, input: Value }, function (Result) {
					ApplyResult(Result);
					$('#AssetInput').val('').trigger('focus');
				}, 'Validating asset...');
			}

			function ResolveSuffix(Suffix) {
				Request('LOOKUP_SUFFIX', { suffix: Suffix }, function (Result) {
					var Choices = Result.choices || [];
					if (!Choices.length) {
						SimpleCallbackModal.Alert('No asset IDs match that suffix.', 'No Match');
					} else if (Choices.length === 1) {
						SubmitInput(Choices[0].value);
					} else {
						SimpleCallbackModal.Choose({
							title: 'Choose Matching Asset',
							label: 'Several asset IDs share that suffix.',
							options: Choices,
							onConfirm: SubmitInput
						});
					}
				}, 'Resolving partial asset ID...');
			}

			function ImportBatch(Code) {
				Request('GET_VALID_ASSETS', { batch_code: Code }, function (Result) {
					var Inputs = (Result.ASSETS || []).map(function (Asset) { return Asset.ASSET_ID; });
					Request('MASS_ADD', { batch_code: CurrentBatch.BATCH_CODE, inputs: Inputs }, ApplyResult, 'Importing batch assets...');
				}, 'Validating source batch...');
			}

			function BuildReport() {
				var Batch = CurrentBatch || {};
				return {
					title: 'Asset Batch ' + (Batch.BATCH_CODE || ''),
					subtitle: Batch.STATUS || '',
					meta: [
						{ label: 'Created', value: (Batch.CREATED_BY || '') + ' | ' + (Batch.CREATED_DATE || '') },
						{ label: 'Updated', value: (Batch.UPDATED_BY || '') + ' | ' + (Batch.UPDATED_DATE || '') },
						{ label: 'Completed', value: (Batch.COMPLETED_BY || '') + ' | ' + (Batch.COMPLETED_DATE || '') },
						{ label: 'Notes', value: Batch.NOTES || '' }
					],
					columns: [
						{ key: 'ASSET_ID', label: 'Asset ID' },
						{ key: 'SERIAL', label: 'Serial' },
						{ key: 'MODEL', label: 'Model' },
						{ key: 'TYPE', label: 'Type' },
						{ key: 'SITE_CODE', label: 'Current Site' }
					],
					rows: CurrentAssets
				};
			}

			$('#BatchCode').on('input', function () { this.value = NormalizeCode(this.value); });
			$('#LoadBatch').on('click', function () { LoadBatch(); });
			$('#CreateBatch').on('click', function () {
				Request('CREATE_BATCH', {}, ApplyResult, 'Creating batch...');
			});
			$('#AddAsset').on('click', function () { SubmitInput($('#AssetInput').val()); });
			$('#AssetInput').on('keydown', function (Event) {
				if (Event.key === 'Enter') {
					Event.preventDefault();
					SubmitInput(this.value);
				}
			});
			$('#MassInput').on('click', function () {
				SimpleCallbackModal.Prompt({
					title: 'Mass Asset Input',
					label: 'Enter one asset ID or serial per line.',
					multiline: true,
					confirmLabel: 'Add Assets',
					onConfirm: function (Value) {
						Request('MASS_ADD', { batch_code: CurrentBatch.BATCH_CODE, inputs: Value }, ApplyResult, 'Processing asset list...');
					}
				});
			});
			$('#CompleteBatch').on('click', function () {
				SimpleCallbackModal.Prompt({
					title: 'Complete Batch',
					label: 'Describe what this batch is ready for.',
					multiline: true,
					confirmLabel: 'Complete',
					onConfirm: function (Notes) {
						Request('COMPLETE_BATCH', { batch_code: CurrentBatch.BATCH_CODE, notes: Notes }, ApplyResult, 'Completing batch...');
					}
				});
			});
			$('#CopyBatch').on('click', function () {
				SimpleCallbackModal.Confirm('Copy this completed batch into a new editable batch?', 'Copy Batch', function () {
					Request('COPY_BATCH', { batch_code: CurrentBatch.BATCH_CODE }, ApplyResult, 'Copying batch...');
				});
			});
			$('#AssetBody').on('click', '.RemoveAsset', function () {
				Request('REMOVE_ASSET', { batch_code: CurrentBatch.BATCH_CODE, asset_id: $(this).data('id') }, ApplyResult, 'Removing asset...');
			});
			$('#PrintBatch').on('click', function () { SimpleReportBuilder.Print(BuildReport()); });
			$('#ExportBatch').on('click', function () {
				SimpleReportBuilder.DownloadCsv(BuildReport(), 'asset-batch-' + CurrentBatch.BATCH_CODE + '.csv');
			});

			RenderAll();
			RefreshChoices();
		});
	</script>
</body>
</html>

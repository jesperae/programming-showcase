<?php
require_once(__DIR__ . '/../04-reusable-operations-toolkit/simple.ajax.endpoint.inc.php');
require_once(__DIR__ . '/demo.devices.inc.php');
require_once(__DIR__ . '/configuration.analyzer.inc.php');

function GetQueryValue($Key, $Default = '')
{
	return isset($_GET[$Key]) ? trim((string)$_GET[$Key]) : $Default;
}

function BuildDeviceBrowser($Devices, $SelectedDevice, $SelectedGroup, $IncludeRules, $ExcludeRules)
{
	$Groups = array_values(array_unique(array_column($Devices, 'DEVICE_GROUP')));
	$Html = "<section class='panel'>";
	$Html .= "<div class='panel-title'>Device Browser</div>";
	$Html .= "<div class='panel-content'>";
	$Html .= "<form method='get' id='AnalyzerForm' class='controls'>";
	$Html .= "<div class='field'><label for='group'>Device Group</label><select id='group' name='group'><option value=''>All groups</option>";
	foreach ($Groups as $Group) {
		$Selected = $Group === $SelectedGroup ? ' selected' : '';
		$Html .= "<option value='" . EncodeHtml($Group) . "'$Selected>" . EncodeHtml($Group) . "</option>";
	}
	$Html .= "</select></div>";
	$Html .= "<div class='field'><label for='device'>Device</label><select id='device' name='device'>";
	foreach ($Devices as $Device) {
		if ($SelectedGroup !== '' && $Device['DEVICE_GROUP'] !== $SelectedGroup) {
			continue;
		}
		$Selected = (int)$Device['KEY_IDX'] === (int)$SelectedDevice ? ' selected' : '';
		$Label = $Device['HOSTNAME'] . ' | ' . $Device['SITE_CODE'] . ' | ' . $Device['MODEL'];
		$Html .= "<option value='" . (int)$Device['KEY_IDX'] . "'$Selected>" . EncodeHtml($Label) . "</option>";
	}
	$Html .= "</select></div>";
	$Html .= "<div class='field grow'><label for='include'>Only Rules Starting With</label><input id='include' name='include' value='" . EncodeHtml($IncludeRules) . "' placeholder='Optional, comma separated'></div>";
	$Html .= "<div class='field grow'><label for='exclude'>Exclude Rules Starting With</label><input id='exclude' name='exclude' value='" . EncodeHtml($ExcludeRules) . "' placeholder='Optional, comma separated'></div>";
	$Html .= "<button type='submit' class='primary'>Analyze</button>";
	$Html .= "</form></div></section>";
	return $Html;
}

function BuildAnalysisDisplay($Device, $Result, $Address)
{
	$Analyzer = new ConfigurationAnalyzer();
	$ContainedBy = $Address !== '' ? $Analyzer->FindContainingNetworks($Address, $Result['NETWORKS']) : [];
	$NetworkText = [];
	foreach ($Result['RULES'] as $Rule) {
		$NetworkText[] = $Rule['NETWORK'] . ' (' . $Rule['RULE'] . ')';
	}

	$Html = "<section class='panel'>";
	$Html .= "<div class='panel-title'>Device: " . EncodeHtml($Device['HOSTNAME']) . "</div>";
	$Html .= "<div class='panel-content'>";
	$Html .= "<div class='summary-grid'>";
	$Html .= "<div><strong>Group:</strong> " . EncodeHtml($Device['DEVICE_GROUP']) . "</div>";
	$Html .= "<div><strong>Site:</strong> " . EncodeHtml($Device['SITE_CODE']) . "</div>";
	$Html .= "<div><strong>Model:</strong> " . EncodeHtml($Device['MODEL']) . "</div>";
	$Html .= "<div><strong>Parser:</strong> " . EncodeHtml($Result['PROFILE']) . "</div>";
	$Html .= "</div>";
	$Html .= "<div class='three-column' style='margin-top:14px'>";
	$Html .= "<div><h3>Permitted Networks <span class='count-badge'>" . count($Result['NETWORKS']) . "</span></h3>";
	$Html .= "<textarea id='NetworkOutput' class='network-list' readonly>" . EncodeHtml(implode("\n", $NetworkText)) . "</textarea>";
	$Html .= "<div class='button-row' style='margin-top:8px'><button type='button' id='CopyNetworks'>Copy Results</button><button type='button' id='ToggleConfig'>View Configuration</button></div></div>";
	$Html .= "<div><h3>Overlapping Networks</h3><textarea class='network-list' readonly>" . EncodeHtml(implode("\n", $Result['OVERLAPS'])) . "</textarea></div>";
	$Html .= "<div><h3>Rule Controls</h3><div class='rule-list'>";
	foreach ($Result['ALL_RULES'] as $Rule) {
		$Html .= "<button type='button' class='rule-option' data-rule='" . EncodeHtml($Rule) . "'>" . EncodeHtml($Rule) . "</button>";
	}
	$Html .= "</div><p class='help-text'>Click a rule to isolate it. Shift-click to exclude it.</p></div>";
	$Html .= "</div>";
	$Html .= "<div id='ConfigPanel' style='display:none;margin-top:14px'><h3>Configuration Snapshot</h3><textarea class='code-box' readonly>" . EncodeHtml($Device['CONFIG_SNAPSHOT']) . "</textarea></div>";
	$Html .= "<form method='get' class='inline-fields' style='margin-top:14px'>";
	$Html .= "<input type='hidden' name='device' value='" . (int)$Device['KEY_IDX'] . "'>";
	$Html .= "<input type='hidden' name='group' value='" . EncodeHtml(GetQueryValue('group')) . "'>";
	$Html .= "<input type='hidden' name='include' value='" . EncodeHtml(GetQueryValue('include')) . "'>";
	$Html .= "<input type='hidden' name='exclude' value='" . EncodeHtml(GetQueryValue('exclude')) . "'>";
	$Html .= "<div class='field'><label for='address'>Address Coverage</label><input id='address' name='address' value='" . EncodeHtml($Address) . "' placeholder='203.0.113.10'></div>";
	$Html .= "<button type='submit'>Check Address</button>";
	if ($Address !== '') {
		$Message = $ContainedBy ? 'Covered by: ' . implode(', ', $ContainedBy) : 'Not covered by a permitted network.';
		$Class = $ContainedBy ? 'ok' : 'error';
		$Html .= "<div class='message $Class'>" . EncodeHtml($Message) . "</div>";
	}
	$Html .= "</form>";
	$Html .= "</div></section>";
	return $Html;
}

$Devices = GetDemoDevices();
$SelectedGroup = strtoupper(GetQueryValue('group'));
$AvailableDevices = $SelectedGroup !== '' ? GetDemoDevicesByGroup($SelectedGroup) : $Devices;
$DefaultDevice = $AvailableDevices[0]['KEY_IDX'] ?? $Devices[0]['KEY_IDX'];
$SelectedDevice = (int)GetQueryValue('device', $DefaultDevice);
$Device = GetDemoDevice($SelectedDevice);
if (!$Device || ($SelectedGroup !== '' && $Device['DEVICE_GROUP'] !== $SelectedGroup)) {
	$Device = GetDemoDevice($DefaultDevice);
	$SelectedDevice = $DefaultDevice;
}

$IncludeRules = GetQueryValue('include');
$ExcludeRules = GetQueryValue('exclude');
$Address = GetQueryValue('address');
$Analyzer = new ConfigurationAnalyzer();
$Result = $Analyzer->Analyze($Device, $IncludeRules, $ExcludeRules);
?>
<!doctype html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title>Network Configuration Analyzer</title>
	<link rel="stylesheet" href="../04-reusable-operations-toolkit/simple.styles.css">
	<script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.callback.modal.js"></script>
	<script src="../04-reusable-operations-toolkit/simple.tooltip.js"></script>
</head>
<body>
	<header class="app-header">
		<div class="app-mark">CA</div>
		<div>
			<h1>Network Configuration Analyzer</h1>
			<div class="app-subtitle">Normalize access policy across configuration families</div>
		</div>
		<nav class="app-nav"><a href="../">Suite Home</a></nav>
	</header>
	<main class="page-shell">
		<?php echo BuildDeviceBrowser($Devices, $SelectedDevice, $SelectedGroup, $IncludeRules, $ExcludeRules); ?>
		<?php echo BuildAnalysisDisplay($Device, $Result, $Address); ?>
	</main>
	<script>
		jQuery(function ($) {
			$('#group').on('change', function () {
				$('#device').removeAttr('name');
				$('#AnalyzerForm').trigger('submit');
			});
			$('#CopyNetworks').on('click', function () {
				navigator.clipboard.writeText($('#NetworkOutput').val() || '').then(function () {
					SimpleCallbackModal.BuildStatusMessage('Copied normalized network results.', true);
				});
			});
			$('#ToggleConfig').on('click', function () {
				$('#ConfigPanel').toggle();
			});
			$('.rule-option').on('click', function (Event) {
				var Rule = $(this).data('rule') || '';
				if (Event.shiftKey) {
					$('#exclude').val(Rule);
					$('#include').val('');
				} else {
					$('#include').val(Rule);
					$('#exclude').val('');
				}
				$('#AnalyzerForm').trigger('submit');
			});
		});
	</script>
</body>
</html>

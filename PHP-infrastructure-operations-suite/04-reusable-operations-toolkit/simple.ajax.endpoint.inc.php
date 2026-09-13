<?php
/*
 * Simple AJAX Endpoint
 * Shared action-based JSON request and response helpers.
 */

define('SIMPLE_AJAX_ENDPOINT_LOADED', 1);
define('SIMPLE_AJAX_RESPONSE_OK_KEY', 'ok');
define('SIMPLE_AJAX_RESPONSE_ERROR_KEY', 'error');
define('SIMPLE_AJAX_RESPONSE_MESSAGE_KEY', 'message');
define('SIMPLE_AJAX_RESPONSE_RESULT_KEY', 'result');

if (session_status() !== PHP_SESSION_ACTIVE) {
	session_start();
}

register_shutdown_function('AjaxShutdownHandler');

function EncodeHtml($Value)
{
	return htmlentities((string)$Value, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
}

function GetDemoUserId()
{
	if (empty($_SESSION['DEMO_USER_ID'])) {
		$_SESSION['DEMO_USER_ID'] = 'demo.operator';
	}

	return $_SESSION['DEMO_USER_ID'];
}

function AjaxShutdownHandler()
{
	$Error = error_get_last();
	if (!$Error || headers_sent()) {
		return;
	}

	$FatalTypes = [E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR, E_USER_ERROR];
	if (!in_array((int)($Error['type'] ?? 0), $FatalTypes, true)) {
		return;
	}

	http_response_code(500);
	header('Content-Type: application/json; charset=utf-8');
	echo json_encode([
		SIMPLE_AJAX_RESPONSE_OK_KEY => false,
		SIMPLE_AJAX_RESPONSE_ERROR_KEY => 'SERVER_ERROR',
		SIMPLE_AJAX_RESPONSE_MESSAGE_KEY => 'The server could not complete the request.'
	]);
}

function BuildAjaxStatusMessage()
{
	return "<div class='ajax-status-wrap'>"
		. "<span id='AjaxStatusLoading' class='ajax-status-loading' style='display:none'></span>"
		. "<span id='AjaxStatusMessage' class='ajax-status-message'></span>"
		. "</div>";
}

function GetAjaxAction()
{
	static $Parsed = null;
	if ($Parsed !== null) {
		return $Parsed;
	}

	$Raw = file_get_contents('php://input');
	$Decoded = json_decode((string)$Raw, true);
	$Request = is_array($Decoded) ? $Decoded : array_merge($_GET, $_POST);
	$Action = strtoupper(trim((string)AjaxGetValue($Request, 'action', '')));
	$Data = AjaxGetValue($Request, 'data', []);

	if (!is_array($Data)) {
		$Data = [];
	}
	if (empty($Data) && !is_array($Decoded)) {
		$Data = $Request;
		AjaxRemoveKey($Data, 'action');
	}

	$Parsed = [
		'ACTION' => $Action,
		'DATA' => $Data,
		'RAW' => $Raw
	];
	return $Parsed;
}

function AjaxNormalizeKey($Key)
{
	return strtolower(trim((string)$Key));
}

function AjaxGetValue($Data, $Key, $Default = null)
{
	if (!is_array($Data)) {
		return $Default;
	}

	$Needle = AjaxNormalizeKey($Key);
	foreach ($Data as $CurrentKey => $Value) {
		if (AjaxNormalizeKey($CurrentKey) === $Needle) {
			return $Value;
		}
	}

	return $Default;
}

function AjaxRemoveKey(&$Data, $Key)
{
	if (!is_array($Data)) {
		return;
	}

	$Needle = AjaxNormalizeKey($Key);
	foreach (array_keys($Data) as $CurrentKey) {
		if (AjaxNormalizeKey($CurrentKey) === $Needle) {
			unset($Data[$CurrentKey]);
		}
	}
}

function AjaxGet($Key, $Default = null)
{
	$Action = GetAjaxAction();
	$Value = AjaxGetValue($Action['DATA'], $Key, $Default);
	return is_string($Value) ? trim($Value) : $Value;
}

function AjaxRequire($Key)
{
	$Value = AjaxGet($Key, null);
	if ($Value === null || $Value === '') {
		AjaxError('Missing required field: ' . $Key, 'MISSING_FIELD', 400);
	}

	return $Value;
}

function AjaxSuccess($Result = [], $Message = '')
{
	AjaxReply(true, $Result, $Message, '', 200);
}

function AjaxError($Message, $Code = 'REQUEST_ERROR', $HttpCode = 400)
{
	AjaxReply(false, [], $Message, $Code, $HttpCode);
}

function AjaxReply($Ok, $Result, $Message, $Error, $HttpCode)
{
	if (!headers_sent()) {
		http_response_code($HttpCode);
		header('Content-Type: application/json; charset=utf-8');
	}

	echo json_encode([
		SIMPLE_AJAX_RESPONSE_OK_KEY => (bool)$Ok,
		SIMPLE_AJAX_RESPONSE_ERROR_KEY => $Error,
		SIMPLE_AJAX_RESPONSE_MESSAGE_KEY => $Message,
		SIMPLE_AJAX_RESPONSE_RESULT_KEY => $Result
	]);
	exit;
}

?>


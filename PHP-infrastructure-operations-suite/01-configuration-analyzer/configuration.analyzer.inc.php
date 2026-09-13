<?php
/*
 * Configuration Analyzer
 * Normalizes permitted networks across parser profiles.
 */

class ConfigurationAnalyzer
{
	function Analyze($Device, $IncludeRules = [], $ExcludeRules = [])
	{
		$Profile = strtoupper(trim((string)($Device['PARSER'] ?? '')));
		$Config = (string)($Device['CONFIG_SNAPSHOT'] ?? '');
		$IncludeRules = $this->NormalizeRuleList($IncludeRules);
		$ExcludeRules = $this->NormalizeRuleList($ExcludeRules);

		switch ($Profile) {
			case 'BRACE_POLICY':
				$Parsed = $this->ParseBracePolicy($Config);
				break;
			case 'ACCESS_LIST':
				$Parsed = $this->ParseAccessList($Config);
				break;
			case 'KEY_VALUE':
				$Parsed = $this->ParseKeyValuePolicy($Config);
				break;
			default:
				$Parsed = ['RULES' => [], 'ALL_RULES' => [], 'ERROR' => 'Unsupported parser profile.'];
		}

		$Rules = [];
		foreach ($Parsed['RULES'] as $Rule) {
			if ($this->RuleIsIncluded($Rule['RULE'], $IncludeRules, $ExcludeRules)) {
				$Rules[] = $Rule;
			}
		}

		$Rules = $this->UniqueRules($Rules);
		usort($Rules, function ($First, $Second) {
			$FirstRange = $this->GetNetworkRange($First['NETWORK']);
			$SecondRange = $this->GetNetworkRange($Second['NETWORK']);
			return ($FirstRange['START'] ?? 0) <=> ($SecondRange['START'] ?? 0);
		});

		$Networks = array_values(array_unique(array_column($Rules, 'NETWORK')));
		return [
			'PROFILE' => $Profile,
			'RULES' => $Rules,
			'ALL_RULES' => $Parsed['ALL_RULES'],
			'NETWORKS' => $Networks,
			'OVERLAPS' => $this->FindOverlaps($Networks),
			'ERROR' => $Parsed['ERROR'] ?? ''
		];
	}

	#region Parser Profiles

	function ParseBracePolicy($Config)
	{
		$PrefixLists = [];
		foreach ($this->GetNamedBlocks($Config, 'prefix-list') as $Block) {
			$PrefixLists[$Block['NAME']] = $this->ExtractNetworks($Block['CONTENT']);
		}

		$Rules = [];
		$AllRules = [];
		foreach ($this->GetNamedBlocks($Config, 'term') as $Block) {
			$Name = $Block['NAME'];
			$AllRules[] = $Name;
			if (!preg_match('/\b(accept|permit)\s*;/i', $Block['CONTENT'])) {
				continue;
			}

			foreach ($this->GetNamedBlocks($Block['CONTENT'], 'source-address', false) as $SourceBlock) {
				foreach ($this->ExtractNetworks($SourceBlock['CONTENT']) as $Network) {
					$Rules[] = $this->BuildRule($Name, $Network, 'source-address');
				}
			}

			if (preg_match_all('/\bsource-prefix-list\s+([A-Z0-9_-]+)\s*;/i', $Block['CONTENT'], $Matches)) {
				foreach ($Matches[1] as $PrefixName) {
					foreach ($PrefixLists[$PrefixName] ?? [] as $Network) {
						$Rules[] = $this->BuildRule($Name, $Network, 'prefix-list ' . $PrefixName);
					}
				}
			}
		}

		return ['RULES' => $Rules, 'ALL_RULES' => array_values(array_unique($AllRules))];
	}

	function ParseAccessList($Config)
	{
		$Rules = [];
		$AllRules = [];
		foreach (preg_split('/\r?\n/', $Config) as $Line) {
			if (!preg_match('/^access-list\s+(\S+)\s+(permit|deny)\s+(\S+)(?:\s+group\s+(\S+))?/i', trim($Line), $Match)) {
				continue;
			}
			$Name = strtoupper($Match[4] ?? $Match[1]);
			$AllRules[] = $Name;
			if (strtolower($Match[2]) === 'permit' && $this->IsValidNetwork($Match[3])) {
				$Rules[] = $this->BuildRule($Name, $Match[3], 'access-list ' . $Match[1]);
			}
		}

		return ['RULES' => $Rules, 'ALL_RULES' => array_values(array_unique($AllRules))];
	}

	function ParseKeyValuePolicy($Config)
	{
		$Rules = [];
		$AllRules = [];
		foreach (preg_split('/\r?\n/', $Config) as $Line) {
			$Fields = [];
			foreach (explode(';', $Line) as $Pair) {
				$Pieces = array_map('trim', explode('=', $Pair, 2));
				if (count($Pieces) === 2) {
					$Fields[strtoupper($Pieces[0])] = $Pieces[1];
				}
			}

			$Name = strtoupper(trim((string)($Fields['POLICY'] ?? '')));
			if ($Name === '') {
				continue;
			}
			$AllRules[] = $Name;
			$Network = trim((string)($Fields['SOURCE'] ?? ''));
			if (strtolower($Fields['ACTION'] ?? '') === 'allow' && $this->IsValidNetwork($Network)) {
				$Rules[] = $this->BuildRule($Name, $Network, 'policy record');
			}
		}

		return ['RULES' => $Rules, 'ALL_RULES' => array_values(array_unique($AllRules))];
	}

	#endregion

	#region Network Analysis

	function FindOverlaps($Networks)
	{
		$Ranges = [];
		foreach ($Networks as $Network) {
			$Range = $this->GetNetworkRange($Network);
			if ($Range) {
				$Ranges[] = $Range;
			}
		}

		$Output = [];
		for ($FirstIdx = 0; $FirstIdx < count($Ranges); $FirstIdx++) {
			for ($SecondIdx = $FirstIdx + 1; $SecondIdx < count($Ranges); $SecondIdx++) {
				$First = $Ranges[$FirstIdx];
				$Second = $Ranges[$SecondIdx];
				if ($First['START'] <= $Second['END'] && $Second['START'] <= $First['END']) {
					$Output[] = $First['NETWORK'] . ' overlaps ' . $Second['NETWORK'];
				}
			}
		}

		return $Output;
	}

	function FindContainingNetworks($Address, $Networks)
	{
		$Address = trim((string)$Address);
		if (strpos($Address, '/') === false) {
			$Address .= '/32';
		}
		$Candidate = $this->GetNetworkRange($Address);
		if (!$Candidate) {
			return [];
		}

		$Output = [];
		foreach ($Networks as $Network) {
			$Range = $this->GetNetworkRange($Network);
			if ($Range && $Candidate['START'] >= $Range['START'] && $Candidate['END'] <= $Range['END']) {
				$Output[] = $Network;
			}
		}

		return $Output;
	}

	function GetNetworkRange($Network)
	{
		$Pieces = explode('/', trim((string)$Network), 2);
		if (count($Pieces) !== 2 || filter_var($Pieces[0], FILTER_VALIDATE_IP, FILTER_FLAG_IPV4) === false) {
			return [];
		}

		$Prefix = filter_var($Pieces[1], FILTER_VALIDATE_INT, ['options' => ['min_range' => 0, 'max_range' => 32]]);
		if ($Prefix === false) {
			return [];
		}

		$Ip = (int)sprintf('%u', ip2long($Pieces[0]));
		$Size = 2 ** (32 - $Prefix);
		$Start = (int)(floor($Ip / $Size) * $Size);
		return [
			'NETWORK' => $Network,
			'START' => $Start,
			'END' => $Start + $Size - 1
		];
	}

	#endregion

	#region Helpers

	function GetNamedBlocks($Text, $Keyword, $RequiresName = true)
	{
		$NamePattern = $RequiresName ? '\\s+([A-Z0-9_-]+)' : '(?:\\s+([A-Z0-9_-]+))?';
		$Pattern = '/\\b' . preg_quote($Keyword, '/') . $NamePattern . '\\s*\\{/i';
		preg_match_all($Pattern, $Text, $Matches, PREG_OFFSET_CAPTURE);
		$Blocks = [];
		foreach ($Matches[0] as $Idx => $FullMatch) {
			$OpenPos = strpos($Text, '{', $FullMatch[1]);
			$ClosePos = $this->FindClosingBrace($Text, $OpenPos);
			if ($OpenPos === false || $ClosePos < 0) {
				continue;
			}
			$Blocks[] = [
				'NAME' => strtoupper($Matches[1][$Idx][0] ?? $Keyword),
				'CONTENT' => substr($Text, $OpenPos + 1, $ClosePos - $OpenPos - 1)
			];
		}

		return $Blocks;
	}

	function FindClosingBrace($Text, $OpenPos)
	{
		if ($OpenPos === false) {
			return -1;
		}

		$Depth = 0;
		for ($Idx = $OpenPos; $Idx < strlen($Text); $Idx++) {
			if ($Text[$Idx] === '{') {
				$Depth++;
			} elseif ($Text[$Idx] === '}') {
				$Depth--;
				if ($Depth === 0) {
					return $Idx;
				}
			}
		}

		return -1;
	}

	function ExtractNetworks($Text)
	{
		preg_match_all('/\b(?:\d{1,3}\.){3}\d{1,3}\/(?:[0-9]|[12][0-9]|3[0-2])\b/', $Text, $Matches);
		return array_values(array_filter(array_unique($Matches[0]), [$this, 'IsValidNetwork']));
	}

	function IsValidNetwork($Network)
	{
		return !empty($this->GetNetworkRange($Network));
	}

	function BuildRule($Name, $Network, $Source)
	{
		return ['RULE' => strtoupper($Name), 'NETWORK' => $Network, 'SOURCE' => $Source];
	}

	function UniqueRules($Rules)
	{
		$Output = [];
		$Seen = [];
		foreach ($Rules as $Rule) {
			$Key = $Rule['RULE'] . '|' . $Rule['NETWORK'];
			if (!isset($Seen[$Key])) {
				$Seen[$Key] = true;
				$Output[] = $Rule;
			}
		}

		return $Output;
	}

	function NormalizeRuleList($Rules)
	{
		if (is_string($Rules)) {
			$Rules = explode(',', $Rules);
		}

		$Output = [];
		foreach ((array)$Rules as $Rule) {
			$Rule = strtoupper(trim((string)$Rule));
			if ($Rule !== '') {
				$Output[] = $Rule;
			}
		}

		return array_values(array_unique($Output));
	}

	function RuleIsIncluded($Rule, $Includes, $Excludes)
	{
		$Rule = strtoupper($Rule);
		foreach ($Excludes as $Prefix) {
			if (strpos($Rule, $Prefix) === 0) {
				return false;
			}
		}
		if (!$Includes) {
			return true;
		}
		foreach ($Includes as $Prefix) {
			if (strpos($Rule, $Prefix) === 0) {
				return true;
			}
		}

		return false;
	}

	#endregion
}

?>

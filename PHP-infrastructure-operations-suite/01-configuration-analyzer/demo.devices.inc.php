<?php
/* Synthetic device catalog and configuration snapshots. */

function GetDemoDevices()
{
	return [
		[
			'KEY_IDX' => 101,
			'DEVICE_GROUP' => 'RETAIL-DEMO',
			'HOSTNAME' => 'branch-edge-01',
			'SITE_CODE' => 'NORTH-01',
			'MODEL' => 'Edge Appliance 100',
			'PARSER' => 'BRACE_POLICY',
			'CONFIG_SNAPSHOT' => "policy-options {\n\tprefix-list TRUSTED-SERVICES {\n\t\t192.0.2.0/28;\n\t\t198.51.100.20/32;\n\t}\n}\nfirewall {\n\tfamily inet {\n\t\tfilter INBOUND {\n\t\t\tterm ADMIN {\n\t\t\t\tfrom { source-address { 203.0.113.0/28; } }\n\t\t\t\tthen { accept; }\n\t\t\t}\n\t\t\tterm SERVICES {\n\t\t\t\tfrom { source-prefix-list TRUSTED-SERVICES; }\n\t\t\t\tthen { accept; }\n\t\t\t}\n\t\t\tterm BLOCK-ALL { then { discard; } }\n\t\t}\n\t}\n}"
		],
		[
			'KEY_IDX' => 102,
			'DEVICE_GROUP' => 'RETAIL-DEMO',
			'HOSTNAME' => 'branch-edge-02',
			'SITE_CODE' => 'EAST-02',
			'MODEL' => 'Edge Appliance 200',
			'PARSER' => 'ACCESS_LIST',
			'CONFIG_SNAPSHOT' => "access-list OFFICE-IN permit 192.0.2.0/24 group OFFICE\naccess-list OFFICE-IN permit 192.0.2.64/26 group SUPPORT\naccess-list OFFICE-IN permit 198.51.100.64/26 group PARTNERS\naccess-list OFFICE-IN deny 0.0.0.0/0 group DEFAULT"
		],
		[
			'KEY_IDX' => 201,
			'DEVICE_GROUP' => 'LAB-DEMO',
			'HOSTNAME' => 'lab-gateway-01',
			'SITE_CODE' => 'LAB-03',
			'MODEL' => 'Lab Gateway',
			'PARSER' => 'KEY_VALUE',
			'CONFIG_SNAPSHOT' => "policy=REMOTE-SUPPORT; action=allow; source=203.0.113.64/27\npolicy=MONITORING; action=allow; source=198.51.100.128/28\npolicy=LOCAL-LAB; action=allow; source=192.0.2.128/25\npolicy=DEFAULT; action=deny; source=0.0.0.0/0"
		],
		[
			'KEY_IDX' => 202,
			'DEVICE_GROUP' => 'LAB-DEMO',
			'HOSTNAME' => 'lab-gateway-02',
			'SITE_CODE' => 'LAB-03',
			'MODEL' => 'Edge Appliance 100',
			'PARSER' => 'BRACE_POLICY',
			'CONFIG_SNAPSHOT' => "firewall {\n\tfilter LAB-IN {\n\t\tterm ENGINEERING {\n\t\t\tfrom { source-address { 192.0.2.144/28; 203.0.113.96/28; } }\n\t\t\tthen { accept; }\n\t\t}\n\t\tterm MONITORING {\n\t\t\tfrom { source-address { 198.51.100.128/28; } }\n\t\t\tthen { accept; }\n\t\t}\n\t}\n}"
		]
	];
}

function GetDemoDevice($KeyIdx)
{
	foreach (GetDemoDevices() as $Device) {
		if ((int)$Device['KEY_IDX'] === (int)$KeyIdx) {
			return $Device;
		}
	}

	return [];
}

function GetDemoDevicesByGroup($Group)
{
	$Group = strtoupper(trim((string)$Group));
	return array_values(array_filter(GetDemoDevices(), function ($Device) use ($Group) {
		return $Device['DEVICE_GROUP'] === $Group;
	}));
}

?>


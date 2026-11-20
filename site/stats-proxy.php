<?php
// Compatibility for pages that still have the first version of stats.js cached.
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
header('Vary: Origin');
$origin = $_SERVER['HTTP_ORIGIN'] ?? '';
$allowed = in_array($origin, ['https://promptowy.com','https://promptowy.github.io'], true);
if ($allowed) header('Access-Control-Allow-Origin: ' . $origin);
$method = $_SERVER['REQUEST_METHOD'];
if ($method !== 'GET' && $method !== 'POST') { http_response_code(405); exit; }
if ($method === 'POST' && (!$allowed || file_get_contents('php://input', false, null, 0, 16) !== 'pageview')) {
    http_response_code(403);
    exit;
}
$curl = curl_init('https://naiman.192-109-244-184.sslip.io/usage');
curl_setopt_array($curl, [CURLOPT_RETURNTRANSFER=>true, CURLOPT_CONNECTTIMEOUT=>2, CURLOPT_TIMEOUT=>5,
    CURLOPT_USERAGENT=>'NAIMAN stats proxy', CURLOPT_HTTPHEADER=>$allowed ? ['Origin: ' . $origin] : []]);
if ($method === 'POST') curl_setopt($curl, CURLOPT_POSTFIELDS, 'pageview');
$body = curl_exec($curl);
$ok = is_string($body) && curl_getinfo($curl, CURLINFO_RESPONSE_CODE) === 200;
curl_close($curl);
http_response_code($ok ? 200 : 503);
echo $ok ? $body : '{"error":"stats unavailable"}';

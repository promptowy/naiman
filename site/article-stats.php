<?php
// WordPress integration for the NAIMAN product article.
if (!defined('ABSPATH')) exit;
add_shortcode('naiman_public_stats', static function () {
    wp_enqueue_script('naiman-public-stats', content_url('/uploads/naiman/stats.js'), [], '0.3.1', true);
    return '<p id="usage-stats"><strong data-stat="views">…</strong> odsłon landingu od 20.11.2026 · '
        . '<strong data-stat="downloads">…</strong> pobrań pliku EXE ze wszystkich wydań.</p>';
});

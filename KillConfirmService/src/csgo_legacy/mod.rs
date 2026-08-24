mod map;
mod player;
mod values;

#[cfg(test)]
mod tests;

use gsi_cs2::Body;
use serde_json::{Map, Value};

use map::{normalize_bomb, normalize_map, normalize_provider, normalize_round};
use player::normalize_player;
use values::{find, normalize_string_map, object};

const GSI_AUTH_TOKEN: &str = "killconfirm";

/// Validate the Legacy authentication section using the same tolerant key
/// matching as the rest of the Legacy adapter. The CS2 path keeps using its
/// original strict authentication check.
pub fn has_valid_auth(value: &Value) -> bool {
    object(value)
        .and_then(|root| find(root, &["auth"]))
        .and_then(object)
        .and_then(|auth| find(auth, &["token"]))
        .and_then(|token| token.as_str())
        == Some(GSI_AUTH_TOKEN)
}

/// Parse a CS:GO Legacy GSI payload without weakening the strict CS2 parser.
///
/// Legacy sends a smaller schema than CS2 and community builds sometimes vary
/// between numeric/string values or key casing. This adapter selects only the
/// fields consumed by Kill Confirm, fills fields that only exist in the CS2
/// model, and then converts the result into the shared event input type.
pub fn parse_body(value: Value) -> Result<Body, serde_json::Error> {
    serde_json::from_value(normalize_payload(&value))
}

fn normalize_payload(value: &Value) -> Value {
    let mut target = Map::new();

    insert_section(&mut target, "provider", value, normalize_provider);
    insert_section(&mut target, "map", value, normalize_map);
    insert_section(&mut target, "player", value, normalize_player);
    insert_section(&mut target, "round", value, normalize_round);
    insert_section(&mut target, "bomb", value, normalize_bomb);

    let auth = object(value)
        .and_then(|root| find(root, &["auth"]))
        .and_then(object)
        .map(normalize_string_map)
        .unwrap_or_default();
    target.insert("auth".to_string(), Value::Object(auth));

    Value::Object(target)
}

fn insert_section(
    target: &mut Map<String, Value>,
    name: &str,
    source: &Value,
    normalize: fn(&Map<String, Value>) -> Value,
) {
    if let Some(section) = object(source)
        .and_then(|root| find(root, &[name]))
        .and_then(object)
    {
        target.insert(name.to_string(), normalize(section));
    }
}

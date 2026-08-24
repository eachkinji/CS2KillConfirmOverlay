use serde_json::{Map, Number, Value};

pub(super) fn normalize_string_map(source: &Map<String, Value>) -> Map<String, Value> {
    let mut target = Map::new();
    for (key, value) in source {
        if let Some(value) = text(Some(value)) {
            target.insert(key.clone(), Value::String(value));
        }
    }
    target
}

pub(super) fn copy_optional_string(
    target: &mut Map<String, Value>,
    target_key: &str,
    source: &Map<String, Value>,
    source_keys: &[&str],
) {
    if let Some(value) = text(find(source, source_keys)) {
        target.insert(target_key.to_string(), Value::String(value));
    }
}

pub(super) fn copy_optional_unsigned(
    target: &mut Map<String, Value>,
    target_key: &str,
    source: &Map<String, Value>,
    source_keys: &[&str],
    max: u64,
) {
    if let Some(value) = unsigned(find(source, source_keys)) {
        target.insert(
            target_key.to_string(),
            Value::Number(Number::from(value.min(max))),
        );
    }
}

pub(super) fn string_value(value: Option<&Value>, fallback: &str) -> Value {
    Value::String(text(value).unwrap_or_else(|| fallback.to_string()))
}

pub(super) fn unsigned_value(value: Option<&Value>, fallback: u64, max: u64) -> Value {
    Value::Number(Number::from(unsigned(value).unwrap_or(fallback).min(max)))
}

pub(super) fn bool_value(value: Option<&Value>, fallback: bool) -> Value {
    Value::Bool(boolean(value).unwrap_or(fallback))
}

fn unsigned(value: Option<&Value>) -> Option<u64> {
    match value? {
        Value::Number(number) => number
            .as_u64()
            .or_else(|| number.as_i64().and_then(|value| u64::try_from(value).ok())),
        Value::String(value) => value.trim().parse::<u64>().ok(),
        Value::Bool(value) => Some(u64::from(*value)),
        _ => None,
    }
}

fn boolean(value: Option<&Value>) -> Option<bool> {
    match value? {
        Value::Bool(value) => Some(*value),
        Value::Number(value) => value.as_u64().map(|value| value != 0),
        Value::String(value) => match value.trim().to_ascii_lowercase().as_str() {
            "true" | "1" | "yes" | "on" => Some(true),
            "false" | "0" | "no" | "off" => Some(false),
            _ => None,
        },
        _ => None,
    }
}

pub(super) fn text(value: Option<&Value>) -> Option<String> {
    match value? {
        Value::String(value) => Some(value.clone()),
        Value::Number(value) => Some(value.to_string()),
        Value::Bool(value) => Some(value.to_string()),
        _ => None,
    }
}

pub(super) fn find<'a>(source: &'a Map<String, Value>, keys: &[&str]) -> Option<&'a Value> {
    for key in keys {
        if let Some(value) = source.get(*key) {
            return Some(value);
        }
    }

    source.iter().find_map(|(candidate, value)| {
        let candidate = canonical_key(candidate);
        keys.iter()
            .any(|key| candidate == canonical_key(key))
            .then_some(value)
    })
}

pub(super) fn canonical_key(value: &str) -> String {
    value
        .chars()
        .filter(|character| character.is_ascii_alphanumeric())
        .flat_map(char::to_lowercase)
        .collect()
}

pub(super) fn object(value: &Value) -> Option<&Map<String, Value>> {
    value.as_object()
}

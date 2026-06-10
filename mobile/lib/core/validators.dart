/// Shared field validators for use across all screens.
/// FA-015: email format + password complexity + required-field checks.
library;

// Hoisted to module level so each passwordValidator call does not allocate
// four new RegExp objects on every keystroke. FA-M08.
final _emailRegex     = RegExp(r'^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$');
final _uppercaseRegex = RegExp(r'[A-Z]');
final _lowercaseRegex = RegExp(r'[a-z]');
final _digitRegex     = RegExp(r'[0-9]');
final _specialRegex   = RegExp(r'[!@#\$%^&*(),.?":{}|<>_\-+=\[\]\\/]');

/// Returns an error string if [value] is null/empty, otherwise null.
String? requiredField(String? value, [String fieldName = 'This field']) {
  if (value == null || value.trim().isEmpty) return '$fieldName is required.';
  return null;
}

/// Validates that [value] is a syntactically valid email address.
String? emailValidator(String? value) {
  if (value == null || value.trim().isEmpty) return 'Email is required.';
  if (!_emailRegex.hasMatch(value.trim()))  return 'Enter a valid email address.';
  return null;
}

/// Validates that [value] meets Cognito's default password policy:
/// min 8 chars, at least one uppercase, one lowercase, one digit, one symbol.
String? passwordValidator(String? value) {
  if (value == null || value.isEmpty) return 'Password is required.';

  final errors = <String>[];

  if (value.length < 8)                    errors.add('at least 8 characters');
  if (!_uppercaseRegex.hasMatch(value))    errors.add('an uppercase letter');
  if (!_lowercaseRegex.hasMatch(value))    errors.add('a lowercase letter');
  if (!_digitRegex.hasMatch(value))        errors.add('a number');
  if (!_specialRegex.hasMatch(value))      errors.add('a special character');

  if (errors.isEmpty) return null;
  return 'Password must contain ${errors.join(', ')}.';
}

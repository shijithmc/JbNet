/// Shared field validators for use across all screens.
/// FA-015: email format + password complexity + required-field checks.
library;

// RFC5322-inspired pattern — permissive but catches obvious mistakes.
final _emailRegex = RegExp(r'^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$');

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

  if (value.length < 8) errors.add('at least 8 characters');
  if (!value.contains(RegExp(r'[A-Z]'))) errors.add('an uppercase letter');
  if (!value.contains(RegExp(r'[a-z]'))) errors.add('a lowercase letter');
  if (!value.contains(RegExp(r'[0-9]'))) errors.add('a number');
  if (!value.contains(RegExp(r'[!@#\$%^&*(),.?":{}|<>_\-+=\[\]\\/]'))) {
    errors.add('a special character');
  }

  if (errors.isEmpty) return null;
  return 'Password must contain ${errors.join(', ')}.';
}

/// Domain exception hierarchy for authentication errors.
///
/// All [CognitoClientException] values are mapped to one of these typed
/// exceptions by [CognitoService]. Screens catch these types directly —
/// no `amazon_cognito_identity_dart_2` imports needed in the presentation
/// layer. FA-012.
sealed class AuthException implements Exception {
  final String code;
  final String message;
  const AuthException({required this.code, required this.message});
}

/// Wrong email or password.
final class InvalidCredentialsException extends AuthException {
  const InvalidCredentialsException()
      : super(
          code: 'NotAuthorizedException',
          message: 'Incorrect email or password.',
        );
}

/// No Cognito user found for this email.
final class UserNotFoundException extends AuthException {
  const UserNotFoundException()
      : super(
          code: 'UserNotFoundException',
          message: 'No account found with that email.',
        );
}

/// Account registered but email not yet confirmed.
final class UserNotConfirmedException extends AuthException {
  const UserNotConfirmedException()
      : super(
          code: 'UserNotConfirmedException',
          message: 'Please verify your email address first.',
        );
}

/// A Cognito user with this email already exists.
final class UsernameExistsException extends AuthException {
  const UsernameExistsException()
      : super(
          code: 'UsernameExistsException',
          message: 'An account with this email already exists.',
        );
}

/// Password does not meet Cognito policy requirements.
final class InvalidPasswordException extends AuthException {
  const InvalidPasswordException()
      : super(
          code: 'InvalidPasswordException',
          message: 'Password must be ≥8 chars with numbers and symbols.',
        );
}

/// Confirmation code does not match.
final class CodeMismatchException extends AuthException {
  const CodeMismatchException()
      : super(
          code: 'CodeMismatchException',
          message: 'Incorrect code. Check your email.',
        );
}

/// Confirmation code has expired.
final class ExpiredCodeException extends AuthException {
  const ExpiredCodeException()
      : super(
          code: 'ExpiredCodeException',
          message: 'Code expired. Request a new one.',
        );
}

/// Too many failed authentication attempts.
final class TooManyAttemptsException extends AuthException {
  const TooManyAttemptsException()
      : super(
          code: 'TooManyFailedAttemptsException',
          message: 'Too many attempts. Please wait and try again.',
        );
}

/// A password reset is required before sign-in can proceed.
final class PasswordResetRequiredException extends AuthException {
  const PasswordResetRequiredException()
      : super(
          code: 'PasswordResetRequiredException',
          message: 'Password reset required. Check your email.',
        );
}

/// Catch-all for unexpected Cognito errors.
final class UnknownAuthException extends AuthException {
  const UnknownAuthException(String errorCode)
      : super(
          code: errorCode,
          message: 'Authentication failed. Please try again.',
        );
}

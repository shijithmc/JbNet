import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'auth_state.dart';
import 'cognito_service.dart';

const _baseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'https://api.jbnet.example.com', // replaced at build time via --dart-define
);

final apiClientProvider = Provider<Dio>((ref) {
  final dio = Dio(BaseOptions(
    baseUrl: _baseUrl,
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 30),
    headers: {'Content-Type': 'application/json'},
  ));

  // ── Auth interceptor ────────────────────────────────────────────────────────
  dio.interceptors.add(InterceptorsWrapper(
    onRequest: (options, handler) {
      final authState = ref.read(authStateProvider);
      if (authState.accessToken != null) {
        options.headers['Authorization'] = 'Bearer ${authState.accessToken}';
      }
      handler.next(options);
    },

    // FA-004: on 401, attempt a silent token refresh and retry the original
    // request exactly once. If refresh fails, sign the user out.
    onError: (error, handler) async {
      if (error.response?.statusCode == 401) {
        // Prevent an infinite retry loop if the refreshed token is also rejected.
        final alreadyRetried = error.requestOptions.extra['retried'] == true;
        if (!alreadyRetried) {
          final cognito = ref.read(cognitoServiceProvider);
          final refreshed = await cognito.refreshSession();
          if (refreshed) {
            final newToken = await cognito.getAccessToken();
            if (newToken != null && newToken.isNotEmpty) {
              final auth = ref.read(authStateProvider);
              if (auth.userId != null) {
                // Sync new token into Riverpod state so future requests use it.
                ref.read(authStateProvider.notifier).setAuthenticated(
                  userId:      auth.userId!,
                  accessToken: newToken,
                );
              }
              // Retry the original request with the refreshed token.
              final retryOpts = error.requestOptions
                ..headers['Authorization'] = 'Bearer $newToken'
                ..extra['retried']         = true;
              final response = await dio.fetch(retryOpts);
              return handler.resolve(response);
            }
          }
        }
        // Refresh failed (or already retried) — sign out and let the router
        // redirect to /auth/login.
        await ref.read(authStateProvider.notifier).signOut();
      }
      handler.next(error);
    },
  ));

  return dio;
});

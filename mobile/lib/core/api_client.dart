import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'auth_state.dart';

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

  // Auth interceptor — injects Bearer token from AuthState
  dio.interceptors.add(InterceptorsWrapper(
    onRequest: (options, handler) {
      final authState = ref.read(authStateProvider);
      if (authState.accessToken != null) {
        options.headers['Authorization'] = 'Bearer ${authState.accessToken}';
      }
      handler.next(options);
    },
    onError: (error, handler) {
      if (error.response?.statusCode == 401) {
        // Token expired — sign out and redirect to login (handled by router redirect)
        ref.read(authStateProvider.notifier).signOut();
      }
      handler.next(error);
    },
  ));

  return dio;
});

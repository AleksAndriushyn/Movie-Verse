import React, { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { useMovie } from "@/hooks/useMovies";
import { getBackdropSrcSet, getImageUrl } from "@/utils/image-helper";
import clsx from "clsx";
import formatRuntime from "@/utils/format-runtime";
import { CastCarousel } from "@/components/Cast/CastCarousel";
import { TrailerModal } from "@/components/modals/TrailerModal/TrailerModal";
import { VideoCarousel } from "@/components/videos/VideoCarousel";
import { MoviePageSkeleton } from "@/components/UI/MoviePageSkeleton";
import { ApiError } from "@/api/AxiosInstance";
import getReleaseYear from "@/utils/release-year";
import NotFoundPage from "./NotFoundPage";
import { ListToggleButtons } from "@/components/Lists/ListToggleButtons";
import { RatingStars } from "@/components/Ratings/RatingStars";
import { ReviewsSection } from "@/components/Reviews/ReviewsSection";
import { MovieShowtimes } from "@/components/Tickets/MovieShowtimes";

const MoviePage: React.FC = () => {
	const { movieId } = useParams<{ movieId: string }>();
	const id = Number(movieId);
	const { data: movie, isLoading, error } = useMovie(id);

	const [isModalOpen, setIsModalOpen] = useState(false);
	const [selectedVideoKey, setSelectedVideoKey] = useState<string | null>(null);

	const handleVideoClick = (key: string) => {
		setSelectedVideoKey(key);
		setIsModalOpen(true);
	};

	const closeModal = () => setIsModalOpen(false);

	useEffect(() => {
		window.scrollTo(0, 0);
	}, [movie]);

	if (isLoading) {
		return <MoviePageSkeleton />
	}

	// a non-numeric id never reaches the API, so treat it as not found too
	if (!movie && (!error || (error instanceof ApiError && error.status === 404))) {
		return <NotFoundPage />;
	}

	if (!movie) {
		return (
			<div className="text-center p-20 text-red-500 text-2xl">
				Couldn't load this movie. Please try again later.
			</div>
		);
	}

	const backdropUrl = getImageUrl(movie.backdrop_path, "w1280");
	const posterUrl = getImageUrl(movie.poster_path, "w500");
	const backdropSrcSet = getBackdropSrcSet(movie.backdrop_path);
	const releaseYear = getReleaseYear(movie.release_date);

	const rating = movie.vote_average.toFixed(1);
	const ratingColor =
		movie.vote_average >= 8
			? "bg-green-600"
			: movie.vote_average >= 6
			? "bg-yellow-600"
			: "bg-red-600";

	const director = movie.credits?.crew.find(
		(member) => member.job === "Director"
	);

	const writers = movie.credits?.crew.filter(
		(member) => member.department === "Writing"
	);

	const topCast = movie.credits?.cast.filter((member) => member.profile_path) || [];

	// the trailer modal embeds a YouTube player, so other hosts can't be played
	const availableVideos =
		movie.videos?.results.filter((video) => video.site === "YouTube") || [];

	return (
		<div>
			<title>{`${movie.title} | MovieVerse`}</title>
			<div className="movie-hero-section">
				{movie.backdrop_path && (
					<picture className="movie-backdrop">
						<source srcSet={backdropSrcSet} sizes="100vw" />
						<img
							src={backdropUrl}
							alt={`${movie.title} backdrop`}
							className="w-full h-full object-cover"
							loading="eager"
							fetchPriority="high"
						/>
					</picture>
				)}

				<div className="movie-gradient-overlay" />
			</div>

			<div className="movie-details-container">
				<div className="movie-poster-wrapper">
					{movie.poster_path ? (
						<img
							src={posterUrl}
							alt={movie.title}
							className="movie-poster"
							loading="eager"
							fetchPriority="high"
						/>
					) : (
						<div className="movie-poster w-full flex items-center justify-center bg-gray-800 text-gray-500 text-center p-4">
							No poster available
						</div>
					)}

					<div
						className={clsx(
							ratingColor,
							"movie-card-rating movie-page-rating-chip"
						)}
					>
						{rating}
					</div>
				</div>

				<div className="md:ml-10 pt-4 mt-8 md:mt-0 flex-1">
					<h1 className="movie-title">{movie.title}</h1>

					<div className="flex items-center flex-wrap gap-x-4 gap-y-2 mb-6">
						{releaseYear && (
							<p className="movie-meta-item">{releaseYear}</p>
						)}
						<p className="movie-meta-item">{formatRuntime(movie.runtime)}</p>
						<div className="flex items-center gap-2 flex-wrap">
							{movie.genres.map((genre) => (
								<span
									key={genre.id}
									className="text-sm bg-gray-700 px-2 py-1 rounded"
								>
									{genre.name}
								</span>
							))}
						</div>
					</div>

					<ListToggleButtons movieId={movie.id} variant="labeled" className="flex-wrap mb-4" />
					<RatingStars movieId={movie.id} className="mb-6" />

					<h2 className="movie-overview-title">Overview</h2>
					<p className="text-gray-300 leading-relaxed max-w-4xl">
						{movie.overview}
					</p>

					{director && (
						<div className="flex items-center mt-2">
							<p className="font-bold text-white mr-2">Director:</p>
							<p className="text-gray-300">{director.name}</p>
						</div>
					)}

					{writers && writers.length > 0 && (
						<div className="flex items-start mt-2">
							<p className="font-bold text-white mr-2 flex-shrink-0">
								{writers.length > 1 ? "Writers:" : "Writer:"}
							</p>
							<p className="text-gray-300">
								{writers.map((writer) => writer.name).join(", ")}
							</p>
						</div>
					)}
				</div>
			</div>

			<MovieShowtimes movieId={movie.id} />

			<div className="px-4 sm:px-6 lg:px-8 mt-2 border-t border-gray-700 pt-8">
				<h2 className="text-3xl font-bold mb-6">Trailers & Teasers</h2>
				<VideoCarousel
					videos={availableVideos}
					onVideoClick={handleVideoClick}
				/>
			</div>

			<div className="movie-cast">
				<h2 className="text-3xl font-bold mb-6">Cast</h2>
				{<CastCarousel cast={topCast} />}
			</div>

			<ReviewsSection movieId={movie.id} />

			<TrailerModal
				isOpen={isModalOpen}
				onClose={closeModal}
				videoKey={selectedVideoKey}
			/>
		</div>
	);
};

export default MoviePage;

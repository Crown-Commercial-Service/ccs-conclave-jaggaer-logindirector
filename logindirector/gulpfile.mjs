import gulp from 'gulp';
import livereload from 'gulp-livereload';
import gulpSass from 'gulp-sass/legacy.js';
import * as dartSass from 'sass';
import postcss from 'gulp-postcss';
import cssnano from 'cssnano';

import { rollup, watch as rollupWatch } from 'rollup';
import { nodeResolve } from '@rollup/plugin-node-resolve';
import commonjs from '@rollup/plugin-commonjs';
import terser from '@rollup/plugin-terser';
import { deleteAsync } from 'del';

const { dest, parallel, series, src, watch } = gulp;
const sass = gulpSass(dartSass);

const govukDir = './node_modules/govuk-frontend/dist/govuk/assets';
const paths = {
    fonts: {
        dest: 'wwwroot/public/assets/fonts'
    },
    images: {
        src: 'wwwroot/assets/images/*',
        dest: 'wwwroot/public/assets/images'
    },
    styles: {
        src: 'wwwroot/assets/stylesheets',
        dest: 'wwwroot/public/assets'
    },
    scripts: {
        src: 'wwwroot/assets/javascript',
        dest: 'wwwroot/public/assets'
    }
};

/* Compile fonts from NPM dependencies */
const packFonts = () => {
    return src(
        [
            `${govukDir}/fonts/*`
        ],
        {
            encoding: false
        }
    )
        .pipe(dest(paths.fonts.dest));
};

/* Compile images from both NPM dependencies and those held locally */
const packImages = () => {
    return src(
        [
            `${govukDir}/images/*`,
            `${paths.images.src}`,
            `!${govukDir}/images/favicon.ico`
        ],
        {
            encoding: false
        }
    )
        .pipe(dest(paths.images.dest));
};

const rollupInputOptions = {
    input: `${paths.scripts.src}/application.mjs`,
    plugins: [
        nodeResolve(),
        commonjs()
    ]
}

const rollupOutputOptions = {
    dir: paths.scripts.dest,
    format: 'cjs',
    plugins: [
        terser()
    ]
}

/* Compile JS scripts with rollup from files held locally */
const buildScripts = async () => {
    let bundle;
    let buildFailed = {
        status: false
    };

    try {
        // Create a bundle
        bundle = await rollup(rollupInputOptions);

        // Generate the output file
        await bundle.write(rollupOutputOptions);
    } catch (error) {
        buildFailed.status = true;
        buildFailed.error = error;
    }

    if (bundle) {
        await bundle.close();
    }

    if (buildFailed.status) {
        throw buildFailed.error;
    }
};

/* Compile SCSS styling from local storage in SCSS */
const buildStyles = () => {
    return src(`${paths.styles.src}/application.scss`)
        .pipe(
            sass({
                includePaths: ['node_modules']
            }).on('error', sass.logError)
        )
        .pipe(postcss([cssnano()]))
        .pipe(gulp.dest(paths.styles.dest));
};

/* Clean down all previously compiled assets - styles and scripts done separately to support hot reloading */
const clearStaticAssets = () => {
    return deleteAsync([
        `${paths.fonts.dest}/**`,
        `${paths.images.dest}/**`,
        `${paths.styles.dest}/application.css`,
        `${paths.scripts.dest}/application.js`
    ]);
};

const clearStyles = () => {
    return deleteAsync([
        `${paths.styles.dest}/application.css`
    ]);
};

const clearScripts = () => {
    return deleteAsync([
        `${paths.scripts.dest}/application.js`
    ]);
};

const clearCompiledAssets = parallel(
    clearStaticAssets,
    clearStyles,
    clearScripts
);

/* Commands to watch scripts and styles for hot reloading */
const watchOptions = {
    ...rollupInputOptions,
    output: [rollupOutputOptions]
};

const watchScripts = async () => {
    const watcher = rollupWatch(watchOptions);

    watcher.on('event', ({ result }) => {
        if (result) {
            result.close();
        }
    });

    watcher.close()
}

const watchStyles = () => {
    return buildStyles().pipe(livereload());
};

const watchAssets = () => {
    livereload.listen(
        {
            port: 45729
        }
    );
    watch(
        [
            `${paths.scripts.src}/**/*.mjs`,
        ],
        series(
            clearScripts,
            watchScripts
        )
    );
    watch(
        [
            `${paths.styles.src}/**/*.scss`
        ],
        series(
            clearStyles,
            watchStyles
        )
    );
};

/* General build action - clear down previous compilation and recompile from scratch */
const build = series(
    clearCompiledAssets,
    parallel(
        packFonts,
        packImages,
        buildScripts,
        buildStyles
    )
);

export default build;
export { build, watchAssets };